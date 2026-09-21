using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Streaming;

namespace SanchesTV.Desktop.P2P;

public sealed class P2pStreamingService : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly P2pCacheManager _cache = new();

    private ClientEngine? _engine;
    private TorrentManager? _manager;
    private IHttpStream? _httpStream;
    private string? _sessionDirectory;
    private P2pFileItem? _selectedFile;
    private bool _disposed;

    public P2pSettings Settings { get; private set; }
    public IReadOnlyList<P2pFileItem> Files { get; private set; } = Array.Empty<P2pFileItem>();
    public string? CurrentTorrentName => _manager?.Name;
    public bool HasActiveSession => _manager is not null;
    public bool IsStreaming => _httpStream is not null;
    public string CacheRoot => _cache.Root;

    public P2pStreamingService()
    {
        Settings = _cache.LoadSettings();
    }

    public async Task ApplySettingsAsync(P2pSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.MaxCacheGb = Math.Clamp(settings.MaxCacheGb, 1, 500);
        settings.MaxUploadKibPerSecond = Math.Clamp(settings.MaxUploadKibPerSecond, 0, 1024 * 1024);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            var requiresEngineRestart =
                Settings.MaxUploadKibPerSecond != settings.MaxUploadKibPerSecond ||
                Settings.AllowPortForwarding != settings.AllowPortForwarding;

            Settings = settings;
            await _cache.SaveSettingsAsync(Settings, cancellationToken);

            if (requiresEngineRestart && _manager is null)
                await RecreateEngineAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        await _cache.PruneAsync(Settings, _sessionDirectory, cancellationToken);
    }

    public async Task<IReadOnlyList<P2pFileItem>> LoadMagnetAsync(
        string magnetText,
        CancellationToken cancellationToken = default)
    {
        if (!MagnetLink.TryParse(magnetText?.Trim(), out var magnet) || magnet is null)
            throw new ArgumentException("Magnet inválido.", nameof(magnetText));

        return await LoadAsync(
            async (engine, savePath) => await engine.AddStreamingAsync(
                magnet,
                savePath,
                new TorrentSettings() with { CreateContainingDirectory = true }),
            cancellationToken);
    }

    public async Task<IReadOnlyList<P2pFileItem>> LoadTorrentFileAsync(
        string torrentPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(torrentPath) || !File.Exists(torrentPath))
            throw new FileNotFoundException("Arquivo .torrent não encontrado.", torrentPath);

        var torrent = await Torrent.LoadAsync(torrentPath);
        return await LoadAsync(
            async (engine, savePath) => await engine.AddStreamingAsync(
                torrent,
                savePath,
                new TorrentSettings() with { CreateContainingDirectory = true }),
            cancellationToken);
    }

    private async Task<IReadOnlyList<P2pFileItem>> LoadAsync(
        Func<ClientEngine, string, Task<TorrentManager>> createManager,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            await StopSessionCoreAsync(clearDownloadedData: !Settings.KeepDownloadedData, cancellationToken);

            await EnsureEngineAsync(cancellationToken);
            _sessionDirectory = _cache.CreateSessionDirectory();

            _manager = await createManager(_engine!, _sessionDirectory);
            await _manager.StartAsync();
            await _manager.WaitForMetadataAsync(cancellationToken);

            Files = _manager.Files
                .Where(f => f.Length > 0)
                .Select(f => new P2pFileItem(f))
                .OrderByDescending(f => f.IsVideo)
                .ThenByDescending(f => f.Length)
                .ThenBy(f => f.Path, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            if (Files.Count == 0)
                throw new InvalidOperationException("O torrent não contém arquivos reproduzíveis.");

            await _cache.PruneAsync(Settings, _sessionDirectory, cancellationToken);
            return Files;
        }
        catch
        {
            await StopSessionCoreAsync(clearDownloadedData: true, CancellationToken.None);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Uri> StartStreamAsync(P2pFileItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            var manager = _manager
                ?? throw new InvalidOperationException("Carregue um magnet ou .torrent primeiro.");

            if (!Files.Contains(item))
                throw new InvalidOperationException("O arquivo selecionado não pertence à sessão atual.");

            _httpStream?.Dispose();
            _httpStream = null;

            foreach (var file in manager.Files)
            {
                await manager.SetFilePriorityAsync(
                    file,
                    ReferenceEquals(file, item.File) ? Priority.Highest : Priority.DoNotDownload);
            }

            _selectedFile = item;

            var provider = manager.StreamProvider
                ?? throw new InvalidOperationException("O modo streaming não está disponível.");

            _httpStream = await provider.CreateHttpStreamAsync(
                item.File,
                Settings.PrebufferBeforePlay,
                cancellationToken);

            return new Uri(_httpStream.FullUri, UriKind.Absolute);
        }
        finally
        {
            _gate.Release();
        }
    }

    public P2pSessionStats GetStats()
    {
        var manager = _manager;
        if (manager is null)
        {
            return new P2pSessionStats(
                "Parado", string.Empty, 0, 0, 0, 0, 0, 0,
                _cache.GetTotalCacheBytes());
        }

        return new P2pSessionStats(
            manager.State.ToString(),
            manager.Name,
            manager.OpenConnections,
            manager.Monitor.DownloadRate,
            manager.Monitor.UploadRate,
            manager.Monitor.DataBytesReceived,
            manager.Monitor.DataBytesSent,
            _selectedFile?.Progress ?? manager.PartialProgress,
            _cache.GetTotalCacheBytes());
    }

    public async Task StopSessionAsync(bool? clearDownloadedData = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopSessionCoreAsync(
                clearDownloadedData ?? !Settings.KeepDownloadedData,
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearInactiveCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cache.ClearInactiveSessionsAsync(_sessionDirectory, cancellationToken);
        await _cache.PruneAsync(Settings, _sessionDirectory, cancellationToken);
    }

    private Task EnsureEngineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _engine is null
            ? RecreateEngineAsync(cancellationToken)
            : Task.CompletedTask;
    }

    private async Task RecreateEngineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_engine is not null)
        {
            try { await _engine.StopAllAsync(); } catch { }
            _engine.Dispose();
            _engine = null;
        }

        var httpPort = GetFreeLoopbackPort();

        var settings = new EngineSettings() with
        {
            AllowLocalPeerDiscovery = false,
            AllowPortForwarding = Settings.AllowPortForwarding,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            CacheDirectory = _cache.EngineCacheRoot,
            DiskCacheBytes = 64 * 1024 * 1024,
            EnableDht = true,
            HttpStreamingPrefix = "http://127.0.0.1:" + httpPort + "/",
            ListenEndPoints = new Dictionary<string, IPEndPoint>
            {
                ["ipv4"] = new(IPAddress.Any, 0)
            }.ToImmutableDictionary(),
            MaximumConnections = 250,
            MaximumDownloadRate = 0,
            MaximumUploadRate = Settings.MaxUploadKibPerSecond <= 0
                ? 0
                : Settings.MaxUploadKibPerSecond * 1024
        };

        _engine = new ClientEngine(settings);
    }

    private async Task StopSessionCoreAsync(bool clearDownloadedData, CancellationToken cancellationToken)
    {
        _httpStream?.Dispose();
        _httpStream = null;
        _selectedFile = null;

        var manager = _manager;
        _manager = null;
        Files = Array.Empty<P2pFileItem>();

        if (manager is not null)
        {
            try { await manager.StopAsync(); } catch { }

            if (_engine is not null)
            {
                try
                {
                    var mode = clearDownloadedData
                        ? RemoveMode.CacheDataAndDownloadedData
                        : RemoveMode.KeepAllData;
                    await _engine.RemoveAsync(manager, mode);
                }
                catch
                {
                    if (clearDownloadedData)
                        P2pCacheManager.TryDeleteDirectory(_sessionDirectory);
                }
            }
        }

        if (clearDownloadedData)
            P2pCacheManager.TryDeleteDirectory(_sessionDirectory);

        _sessionDirectory = null;
        await _cache.PruneAsync(Settings, activeSessionDirectory: null, cancellationToken);
    }

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(P2pStreamingService));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await _gate.WaitAsync();
        try
        {
            await StopSessionCoreAsync(
                clearDownloadedData: !Settings.KeepDownloadedData,
                CancellationToken.None);

            if (_engine is not null)
            {
                try { await _engine.StopAllAsync(); } catch { }
                _engine.Dispose();
                _engine = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}

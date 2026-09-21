using System.Buffers;
using System.Collections.Immutable;
using System.IO;
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

    private long _lastReceivedBytes;
    private DateTimeOffset _lastDataUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastRecoveryUtc = DateTimeOffset.MinValue;
    private int _recoveryCount;
    private string _lastRecoveryMessage = "nenhuma";

    public P2pSettings Settings { get; private set; }
    public IReadOnlyList<P2pFileItem> Files { get; private set; } = Array.Empty<P2pFileItem>();
    public string? CurrentTorrentName => _manager?.Name;
    public bool HasActiveSession => _manager is not null;
    public bool IsStreaming => _httpStream is not null;
    public string CacheRoot => _cache.Root;

    public P2pStreamingService()
    {
        Settings = NormalizeSettings(_cache.LoadSettings());
    }

    public async Task ApplySettingsAsync(P2pSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = NormalizeSettings(settings);

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
        var value = magnetText?.Trim();
        if (string.IsNullOrWhiteSpace(value) || !MagnetLink.TryParse(value, out var magnet) || magnet is null)
            throw new ArgumentException("Magnet inválido.", nameof(magnetText));

        return await LoadAsync(
            magnet.InfoHashes.V1OrV2.ToHex(),
            async (engine, savePath) => await engine.AddStreamingAsync(
                magnet,
                savePath,
                CreateTorrentSettings()),
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
            torrent.InfoHashes.V1OrV2.ToHex(),
            async (engine, savePath) => await engine.AddStreamingAsync(
                torrent,
                savePath,
                CreateTorrentSettings()),
            cancellationToken);
    }

    private async Task<IReadOnlyList<P2pFileItem>> LoadAsync(
        string sessionKey,
        Func<ClientEngine, string, Task<TorrentManager>> createManager,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            await StopSessionCoreAsync(clearDownloadedData: !Settings.KeepDownloadedData, cancellationToken);

            await EnsureEngineAsync(cancellationToken);
            _sessionDirectory = _cache.CreateSessionDirectory(
                sessionKey,
                reset: !Settings.KeepDownloadedData);

            _manager = await createManager(_engine!, _sessionDirectory);
            await _manager.StartAsync();

            using var metadataCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            metadataCts.CancelAfter(TimeSpan.FromSeconds(Settings.MetadataTimeoutSeconds));
            try
            {
                await _manager.WaitForMetadataAsync(metadataCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"O torrent não entregou os metadados em {Settings.MetadataTimeoutSeconds}s. " +
                    "Tente outra fonte ou aguarde mais seeders.");
            }

            Files = _manager.Files
                .Where(f => f.Length > 0)
                .Select(f => new P2pFileItem(f))
                .OrderByDescending(f => f.IsVideo)
                .ThenByDescending(f => f.Length)
                .ThenBy(f => f.Path, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            if (Files.Count == 0)
                throw new InvalidOperationException("O torrent não contém arquivos reproduzíveis.");

            _lastReceivedBytes = _manager.Monitor.DataBytesReceived;
            _lastDataUtc = DateTimeOffset.UtcNow;
            _lastRecoveryUtc = DateTimeOffset.MinValue;
            _recoveryCount = 0;
            _lastRecoveryMessage = "nenhuma";

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
                var priority = ReferenceEquals(file, item.File)
                    ? Priority.Highest
                    : Priority.DoNotDownload;
                await manager.SetFilePriorityAsync(file, priority);
            }

            _selectedFile = item;

            var provider = manager.StreamProvider
                ?? throw new InvalidOperationException("O modo streaming não está disponível.");

            if (Settings.PrebufferBeforePlay && item.Length > 0)
                await WarmInitialBufferAsync(provider, item, cancellationToken);

            _httpStream = await provider.CreateHttpStreamAsync(
                item.File,
                prebuffer: false,
                cancellationToken);

            _lastReceivedBytes = manager.Monitor.DataBytesReceived;
            _lastDataUtc = DateTimeOffset.UtcNow;
            return new Uri(_httpStream.FullUri, UriKind.Absolute);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WarmInitialBufferAsync(
        StreamProvider provider,
        P2pFileItem item,
        CancellationToken cancellationToken)
    {
        var targetBytes = Math.Min(
            item.Length,
            Math.Max(1, Settings.InitialBufferMb) * 1024L * 1024L);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Settings.StartBufferTimeoutSeconds));

        Stream? warmup = null;
        byte[]? buffer = null;
        long total = 0;

        try
        {
            warmup = await provider.CreateStreamAsync(item.File, prebuffer: true, timeoutCts.Token);
            buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);

            while (total < targetBytes)
            {
                var wanted = (int)Math.Min(buffer.Length, targetBytes - total);
                var read = await warmup.ReadAsync(buffer.AsMemory(0, wanted), timeoutCts.Token);
                if (read <= 0)
                    break;

                total += read;
                _lastDataUtc = DateTimeOffset.UtcNow;
            }

            _lastRecoveryMessage = $"buffer inicial {total / 1_048_576d:N1} MB";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _lastRecoveryMessage =
                $"buffer parcial após {Settings.StartBufferTimeoutSeconds}s; reprodução iniciada com o que já chegou";
        }
        finally
        {
            warmup?.Dispose();
            if (buffer is not null)
                ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task MaintainAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is null || !Settings.AutoRecovery)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var manager = _manager;
            if (manager is null)
                return;

            var received = manager.Monitor.DataBytesReceived;
            if (received > _lastReceivedBytes)
            {
                _lastReceivedBytes = received;
                _lastDataUtc = DateTimeOffset.UtcNow;
                return;
            }

            if (_selectedFile is null || _selectedFile.Progress >= 99.9)
                return;

            var now = DateTimeOffset.UtcNow;
            var stalledFor = now - _lastDataUtc;
            var recoveryCooldown = now - _lastRecoveryUtc;

            if (stalledFor < TimeSpan.FromSeconds(Settings.StallRecoverySeconds) ||
                recoveryCooldown < TimeSpan.FromSeconds(Math.Max(8, Settings.StallRecoverySeconds / 2)))
                return;

            if (manager.Monitor.DownloadRate > 96 * 1024 && manager.OpenConnections > 0)
                return;

            await RecoverCoreAsync(
                $"sem dados por {stalledFor.TotalSeconds:N0}s",
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecoverNowAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            if (_manager is null)
                throw new InvalidOperationException("Não há sessão P2P ativa.");

            await RecoverCoreAsync("recuperação manual", cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RecoverCoreAsync(string reason, CancellationToken cancellationToken)
    {
        var manager = _manager;
        if (manager is null)
            return;

        _lastRecoveryUtc = DateTimeOffset.UtcNow;
        _recoveryCount++;
        _lastRecoveryMessage = reason;

        if (manager.State is TorrentState.Stopped or TorrentState.Error)
        {
            try
            {
                await manager.StartAsync();
            }
            catch
            {
                // Continue with reannounce attempts below.
            }
        }

        if (_selectedFile is not null)
        {
            try
            {
                await manager.SetFilePriorityAsync(_selectedFile.File, Priority.Highest);
            }
            catch
            {
            }
        }

        try
        {
            await manager.TrackerManager.AnnounceAsync(cancellationToken);
        }
        catch
        {
        }

        try
        {
            await manager.DhtAnnounceAsync();
        }
        catch
        {
        }
    }

    public P2pSessionStats GetStats()
    {
        var manager = _manager;
        if (manager is null)
        {
            return new P2pSessionStats(
                "Parado", string.Empty, 0, 0, 0, 0, 0, 0,
                _cache.GetTotalCacheBytes(),
                "Parado", _recoveryCount, _lastRecoveryMessage);
        }

        var idle = DateTimeOffset.UtcNow - _lastDataUtc;
        var selectedProgress = _selectedFile?.Progress ?? manager.PartialProgress;

        var health = selectedProgress >= 99.9
            ? "Pronto"
            : manager.OpenConnections == 0
                ? "Procurando peers"
                : manager.Monitor.DownloadRate <= 16 * 1024 &&
                  idle >= TimeSpan.FromSeconds(Settings.StallRecoverySeconds)
                    ? "Recuperando"
                    : manager.Monitor.DownloadRate <= 96 * 1024
                        ? "Lento"
                        : "Saudável";

        return new P2pSessionStats(
            manager.State.ToString(),
            manager.Name,
            manager.OpenConnections,
            manager.Monitor.DownloadRate,
            manager.Monitor.UploadRate,
            manager.Monitor.DataBytesReceived,
            manager.Monitor.DataBytesSent,
            selectedProgress,
            _cache.GetTotalCacheBytes(),
            health,
            _recoveryCount,
            _lastRecoveryMessage);
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

        var settings = new EngineSettingsBuilder
        {
            AllowLocalPeerDiscovery = true,
            AllowPortForwarding = Settings.AllowPortForwarding,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            CacheDirectory = _cache.EngineCacheRoot,
            DiskCacheBytes = 128 * 1024 * 1024,
            DhtEndPoint = new IPEndPoint(IPAddress.Any, 0),
            HttpStreamingPrefix = "http://127.0.0.1:" + httpPort + "/",
            ListenEndPoints = new Dictionary<string, IPEndPoint>
            {
                ["ipv4"] = new(IPAddress.Any, 0)
            },
            MaximumConnections = 500,
            MaximumHalfOpenConnections = 32,
            MaximumDownloadRate = 0,
            MaximumUploadRate = Settings.MaxUploadKibPerSecond <= 0
                ? 0
                : Settings.MaxUploadKibPerSecond * 1024,
            StaleRequestTimeout = TimeSpan.FromSeconds(24)
        }.ToSettings();

        _engine = new ClientEngine(settings);
    }

    private TorrentSettings CreateTorrentSettings()
    {
        return new TorrentSettingsBuilder
        {
            AllowDht = true,
            AllowPeerExchange = true,
            CreateContainingDirectory = true,
            MaximumConnections = 220,
            MaximumDownloadRate = 0,
            MaximumUploadRate = Settings.MaxUploadKibPerSecond <= 0
                ? 0
                : Settings.MaxUploadKibPerSecond * 1024,
            UploadSlots = 12
        }.ToSettings();
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
        _lastReceivedBytes = 0;
        _lastDataUtc = DateTimeOffset.UtcNow;
        await _cache.PruneAsync(Settings, activeSessionDirectory: null, cancellationToken);
    }

    private static P2pSettings NormalizeSettings(P2pSettings settings)
    {
        settings.MaxCacheGb = Math.Clamp(settings.MaxCacheGb, 1, 500);
        settings.MaxUploadKibPerSecond = Math.Clamp(settings.MaxUploadKibPerSecond, 0, 1024 * 1024);
        settings.InitialBufferMb = Math.Clamp(settings.InitialBufferMb, 4, 256);
        settings.MetadataTimeoutSeconds = Math.Clamp(settings.MetadataTimeoutSeconds, 15, 300);
        settings.StartBufferTimeoutSeconds = Math.Clamp(settings.StartBufferTimeoutSeconds, 20, 600);
        settings.StallRecoverySeconds = Math.Clamp(settings.StallRecoverySeconds, 6, 120);
        return settings;
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

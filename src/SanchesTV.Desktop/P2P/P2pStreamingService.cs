using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace SanchesTV.Desktop.P2P;

public sealed record P2pFile(int Index, string Name, long Length, double Progress)
{
    public string DisplaySize => Length switch
    {
        >= 1_073_741_824 => $"{Length / 1_073_741_824d:N2} GB",
        >= 1_048_576 => $"{Length / 1_048_576d:N1} MB",
        _ => $"{Length / 1024d:N0} KB"
    };
}

public sealed record P2pSession(
    string InfoHash,
    string Name,
    IReadOnlyList<P2pFile> Files,
    double DownloadSpeed,
    long Peers);

public sealed class P2pStreamingService : IAsyncDisposable
{
    private static readonly string[] VideoExtensions =
    [".mkv", ".mp4", ".avi", ".webm", ".mov", ".m4v", ".ts", ".m2ts", ".mpg", ".mpeg"];

    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://127.0.0.1:11470/"), Timeout = TimeSpan.FromSeconds(45) };
    private Process? _process;

    public string ExecutablePath => Path.Combine(AppContext.BaseDirectory, "runtime", "p2p", "stream-server.exe");
    public bool IsAvailable => File.Exists(ExecutablePath);

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        if (await IsHealthyAsync(cancellationToken))
            return;

        if (!IsAvailable)
            throw new FileNotFoundException("O motor P2P não foi encontrado no instalador.", ExecutablePath);

        if (_process is null or { HasExited: true })
        {
            var dataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SanchesTV", "P2P");

            Directory.CreateDirectory(dataRoot);
            _process?.Dispose();
            _process = Process.Start(new ProcessStartInfo
            {
                FileName = ExecutablePath,
                WorkingDirectory = dataRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
        }

        for (var i = 0; i < 40; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsHealthyAsync(cancellationToken))
                return;
            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException("O motor P2P não respondeu em localhost:11470.");
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("heartbeat", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<P2pSession> AddMagnetAsync(string magnet, CancellationToken cancellationToken = default)
    {
        if (!magnet.StartsWith("magnet:?xt=urn:btih:", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Informe um magnet BitTorrent válido.");

        await EnsureStartedAsync(cancellationToken);

        using var response = await _http.PostAsJsonAsync("create", new
        {
            from = magnet,
            guessFileIdx = true,
            fileMustInclude = Array.Empty<string>()
        }, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (doc.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException(error.GetString() ?? "Falha ao adicionar torrent.");

        return ParseSession(doc.RootElement);
    }

    public async Task<P2pSession> RefreshAsync(string infoHash, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"{Uri.EscapeDataString(infoHash)}/stats.json", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return ParseSession(doc.RootElement);
    }

    public Uri GetStreamUri(string infoHash, int fileIndex) =>
        new($"http://127.0.0.1:11470/stream/{Uri.EscapeDataString(infoHash)}/{fileIndex}");

    public async Task RemoveAsync(string infoHash, CancellationToken cancellationToken = default)
    {
        try { await _http.GetAsync($"{Uri.EscapeDataString(infoHash)}/remove", cancellationToken); }
        catch { }
    }

    public static bool IsVideo(P2pFile file) =>
        VideoExtensions.Contains(Path.GetExtension(file.Name), StringComparer.OrdinalIgnoreCase);

    private static P2pSession ParseSession(JsonElement root)
    {
        var hash = root.TryGetProperty("infoHash", out var ih) ? ih.GetString() ?? "" : "";
        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "Torrent" : "Torrent";
        var speed = root.TryGetProperty("downloadSpeed", out var ds) && ds.TryGetDouble(out var d) ? d : 0;
        var peers = root.TryGetProperty("peers", out var ps) && ps.TryGetInt64(out var p) ? p : 0;

        var files = new List<P2pFile>();
        if (root.TryGetProperty("files", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var file in array.EnumerateArray())
            {
                var fileName = file.TryGetProperty("name", out var fn)
                    ? fn.GetString() ?? $"Arquivo {index + 1}"
                    : $"Arquivo {index + 1}";
                var length = file.TryGetProperty("length", out var len) && len.TryGetInt64(out var l) ? l : 0;
                var progress = file.TryGetProperty("progress", out var pr) && pr.TryGetDouble(out var pg) ? pg : 0;
                files.Add(new P2pFile(index++, fileName, length, progress));
            }
        }

        return new P2pSession(hash, name, files, speed, peers);
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); } catch { }
        }
        _process?.Dispose();
        await Task.CompletedTask;
    }
}

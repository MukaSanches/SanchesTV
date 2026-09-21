using System.Diagnostics;
using System.IO;
using SanchesTV.Core.Models;

namespace SanchesTV.Desktop.Playback;

public sealed class TimeshiftService : IAsyncDisposable
{
    private readonly string _ffmpegPath = Path.Combine(AppContext.BaseDirectory, "runtime", "ffmpeg", "ffmpeg.exe");
    private Process? _process;
    private string? _sessionDir;

    public bool IsRunning => _process is { HasExited: false };
    public Uri? PlaylistUri { get; private set; }

    public async Task<Uri> StartAsync(ChannelSource source, CancellationToken cancellationToken = default)
    {
        await StopAsync();

        if (!File.Exists(_ffmpegPath))
            throw new FileNotFoundException("FFmpeg não encontrado.", _ffmpegPath);

        _sessionDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SanchesTV", "Timeshift", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sessionDir);

        var playlist = Path.Combine(_sessionDir, "live.m3u8");
        _process = StartProcess(source, playlist, transcode: false);

        var ready = await WaitForPlaylistAsync(playlist, TimeSpan.FromSeconds(12), cancellationToken);
        if (!ready && _process.HasExited)
        {
            _process.Dispose();
            _process = StartProcess(source, playlist, transcode: true);
            ready = await WaitForPlaylistAsync(playlist, TimeSpan.FromSeconds(20), cancellationToken);
        }

        if (!ready)
            throw new TimeoutException("O timeshift não conseguiu criar o buffer inicial.");

        PlaylistUri = new Uri(playlist, UriKind.Absolute);
        return PlaylistUri;
    }

    public long GetBufferedBytes()
    {
        if (_sessionDir is null || !Directory.Exists(_sessionDir))
            return 0;
        try
        {
            return Directory.EnumerateFiles(_sessionDir, "*", SearchOption.TopDirectoryOnly)
                .Select(x => new FileInfo(x))
                .Sum(x => x.Length);
        }
        catch { return 0; }
    }

    private Process StartProcess(ChannelSource source, string playlist, bool transcode)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        foreach (var arg in BuildInputHeaders(source))
            psi.ArgumentList.Add(arg);

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(source.Url.ToString());
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:v:0?");
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:a:0?");

        if (transcode)
        {
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add("h264_mf");
            psi.ArgumentList.Add("-b:v");
            psi.ArgumentList.Add("4500k");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("aac");
            psi.ArgumentList.Add("-b:a");
            psi.ArgumentList.Add("160k");
        }
        else
        {
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("copy");
        }

        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("hls");
        psi.ArgumentList.Add("-hls_time");
        psi.ArgumentList.Add("2");
        psi.ArgumentList.Add("-hls_list_size");
        psi.ArgumentList.Add("900");
        psi.ArgumentList.Add("-hls_flags");
        psi.ArgumentList.Add("delete_segments+append_list+omit_endlist+program_date_time");
        psi.ArgumentList.Add("-hls_delete_threshold");
        psi.ArgumentList.Add("60");
        psi.ArgumentList.Add("-hls_segment_filename");
        psi.ArgumentList.Add(Path.Combine(_sessionDir!, "segment-%06d.ts"));
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add(playlist);

        return Process.Start(psi)
            ?? throw new InvalidOperationException("Não foi possível iniciar o FFmpeg do timeshift.");
    }

    private static IEnumerable<string> BuildInputHeaders(ChannelSource source)
    {
        var list = new List<string> { "-hide_banner", "-loglevel", "warning" };
        if (!string.IsNullOrWhiteSpace(source.UserAgent))
            list.AddRange(["-user_agent", source.UserAgent]);

        var headers = new List<string>();
        if (!string.IsNullOrWhiteSpace(source.Referrer))
            headers.Add($"Referer: {source.Referrer}");
        if (!string.IsNullOrWhiteSpace(source.Origin))
            headers.Add($"Origin: {source.Origin}");
        if (headers.Count > 0)
            list.AddRange(["-headers", string.Join("\r\n", headers) + "\r\n"]);
        return list;
    }

    private static async Task<bool> WaitForPlaylistAsync(
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < until)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(path) &&
                    new FileInfo(path).Length > 30 &&
                    File.ReadAllText(path).Contains("#EXTINF", StringComparison.Ordinal))
                    return true;
            }
            catch { }

            await Task.Delay(350, cancellationToken);
        }
        return false;
    }

    public async Task StopAsync()
    {
        var p = _process;
        _process = null;
        if (p is not null)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                    await p.WaitForExitAsync();
                }
            }
            catch { }
            p.Dispose();
        }

        PlaylistUri = null;
        if (_sessionDir is { } dir)
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
        _sessionDir = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

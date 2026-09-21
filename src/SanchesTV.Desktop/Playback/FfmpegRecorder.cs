using System.Diagnostics;
using SanchesTV.Core.Models;

namespace SanchesTV.Desktop.Playback;

public sealed class FfmpegRecorder : IDisposable
{
    private Process? _process;

    public string ExecutablePath =>
        Path.Combine(AppContext.BaseDirectory, "runtime", "ffmpeg", "ffmpeg.exe");

    public bool IsAvailable => File.Exists(ExecutablePath);
    public bool IsRecording => _process is { HasExited: false };
    public string? OutputPath { get; private set; }

    public bool Start(ChannelSource source, string directory)
    {
        if (!IsAvailable || IsRecording)
            return false;

        Directory.CreateDirectory(directory);
        OutputPath = Path.Combine(directory, $"SanchesTV-{DateTime.Now:yyyyMMdd-HHmmss}.mkv");

        var psi = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false
        };

        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("warning");

        if (!string.IsNullOrWhiteSpace(source.UserAgent))
        {
            psi.ArgumentList.Add("-user_agent");
            psi.ArgumentList.Add(source.UserAgent);
        }

        var headers = new List<string>();
        if (!string.IsNullOrWhiteSpace(source.Referrer))
            headers.Add($"Referer: {source.Referrer}");
        if (!string.IsNullOrWhiteSpace(source.Origin))
            headers.Add($"Origin: {source.Origin}");

        if (headers.Count > 0)
        {
            psi.ArgumentList.Add("-headers");
            psi.ArgumentList.Add(string.Join("\r\n", headers) + "\r\n");
        }

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(source.Url.ToString());
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("copy");
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add(OutputPath);

        _process = Process.Start(psi);
        return _process is not null;
    }

    public void Stop()
    {
        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _process.StandardInput.WriteLine("q");
                _process.StandardInput.Flush();
                if (!_process.WaitForExit(3500))
                    _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            try { _process.Kill(entireProcessTree: true); } catch { }
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new FileNotFoundException("ffmpeg.exe não foi encontrado.", ExecutablePath);

        var psi = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-version");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Não foi possível iniciar FFmpeg.");
        var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return line ?? "FFmpeg";
    }

    public void Dispose() => Stop();
}

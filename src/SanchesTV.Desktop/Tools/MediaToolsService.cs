using System.Diagnostics;
using System.IO;
using SanchesTV.Core.Models;

namespace SanchesTV.Desktop.Tools;

public sealed class MediaToolsService
{
    public string FfmpegPath => Path.Combine(AppContext.BaseDirectory, "runtime", "ffmpeg", "ffmpeg.exe");
    public string FfprobePath => Path.Combine(AppContext.BaseDirectory, "runtime", "ffmpeg", "ffprobe.exe");
    public string MediaMtxPath => Path.Combine(AppContext.BaseDirectory, "runtime", "mediamtx", "mediamtx.exe");
    public string WhisperPath => Path.Combine(AppContext.BaseDirectory, "runtime", "whisper", "whisper-cli.exe");
    public string WhisperModelPath => Path.Combine(AppContext.BaseDirectory, "runtime", "whisper", "ggml-tiny.bin");

    public string? TsAnalyzePath => FindExecutable(
        Path.Combine(AppContext.BaseDirectory, "runtime", "tsduck"),
        "tsanalyze.exe");

    public string? CcExtractorPath => FindExecutable(
        Path.Combine(AppContext.BaseDirectory, "runtime", "ccextractor"),
        "ccextractorwinfull.exe");

    public string GetStatus()
    {
        return string.Join(Environment.NewLine,
            $"FFmpeg: {(File.Exists(FfmpegPath) ? "OK" : "ausente")}",
            $"FFprobe: {(File.Exists(FfprobePath) ? "OK" : "ausente")}",
            $"MediaMTX: {(File.Exists(MediaMtxPath) ? "OK" : "ausente")}",
            $"TSDuck: {(TsAnalyzePath is not null ? "OK" : "ausente")}",
            $"CCExtractor: {(CcExtractorPath is not null ? "OK" : "ausente")}",
            $"whisper.cpp: {(File.Exists(WhisperPath) && File.Exists(WhisperModelPath) ? "OK" : "ausente")}");
    }

    public async Task<string> ProbeAsync(ChannelSource source, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FfprobePath))
            throw new FileNotFoundException("ffprobe.exe não encontrado.", FfprobePath);

        var args = new List<string>
        {
            "-v", "error",
            "-show_format",
            "-show_streams",
            "-print_format", "json"
        };

        if (!string.IsNullOrWhiteSpace(source.UserAgent))
        {
            args.Add("-user_agent");
            args.Add(source.UserAgent);
        }

        var headers = new List<string>();
        if (!string.IsNullOrWhiteSpace(source.Referrer))
            headers.Add($"Referer: {source.Referrer}");
        if (!string.IsNullOrWhiteSpace(source.Origin))
            headers.Add($"Origin: {source.Origin}");
        if (headers.Count > 0)
        {
            args.Add("-headers");
            args.Add(string.Join("\r\n", headers) + "\r\n");
        }

        args.Add(source.Url.ToString());
        return await RunAsync(FfprobePath, args, TimeSpan.FromSeconds(30), cancellationToken);
    }

    public async Task<string> AnalyzeTransportStreamAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var exe = TsAnalyzePath
            ?? throw new FileNotFoundException("TSDuck tsanalyze.exe não encontrado.");
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Arquivo não encontrado.", filePath);

        return await RunAsync(exe, [filePath], TimeSpan.FromMinutes(2), cancellationToken);
    }

    public async Task<string> ExtractCaptionsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var exe = CcExtractorPath
            ?? throw new FileNotFoundException("CCExtractor não encontrado.");
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Arquivo não encontrado.", filePath);

        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "SanchesTV", "Legendas");
        Directory.CreateDirectory(outputDir);
        var output = Path.Combine(
            outputDir,
            Path.GetFileNameWithoutExtension(filePath) + "-captions.srt");

        var result = await RunAsync(
            exe,
            [filePath, "-out=srt", "-o", output],
            TimeSpan.FromMinutes(10),
            cancellationToken);

        return File.Exists(output)
            ? output
            : "CCExtractor concluiu sem gerar SRT.\n" + result;
    }

    public async Task<string> TranscribePtBrAsync(string mediaPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(mediaPath))
            throw new FileNotFoundException("Arquivo não encontrado.", mediaPath);
        if (!File.Exists(FfmpegPath))
            throw new FileNotFoundException("FFmpeg não encontrado.", FfmpegPath);
        if (!File.Exists(WhisperPath) || !File.Exists(WhisperModelPath))
            throw new FileNotFoundException("whisper.cpp ou modelo não encontrado.");

        var outDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "SanchesTV", "Legendas");
        Directory.CreateDirectory(outDir);

        var tempWav = Path.Combine(Path.GetTempPath(), $"sanchestv-whisper-{Guid.NewGuid():N}.wav");
        var outputBase = Path.Combine(
            outDir,
            Path.GetFileNameWithoutExtension(mediaPath) + "-whisper-ptbr");

        try
        {
            await RunAsync(
                FfmpegPath,
                ["-hide_banner", "-loglevel", "error", "-y", "-i", mediaPath,
                 "-vn", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", tempWav],
                TimeSpan.FromMinutes(10),
                cancellationToken);

            await RunAsync(
                WhisperPath,
                ["-m", WhisperModelPath, "-f", tempWav, "-l", "pt",
                 "-osrt", "-otxt", "-of", outputBase, "-np"],
                TimeSpan.FromMinutes(30),
                cancellationToken);

            var srt = outputBase + ".srt";
            if (!File.Exists(srt))
                throw new InvalidOperationException("whisper.cpp terminou sem gerar o SRT esperado.");
            return srt;
        }
        finally
        {
            try { File.Delete(tempWav); } catch { }
        }
    }

    public async Task<string> ProbeLocalFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FfprobePath))
            throw new FileNotFoundException("ffprobe.exe não encontrado.", FfprobePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Arquivo não encontrado.", filePath);

        return await RunAsync(
            FfprobePath,
            ["-v", "error", "-show_format", "-show_streams", "-print_format", "json", filePath],
            TimeSpan.FromSeconds(30),
            cancellationToken);
    }

    public async Task<string> GetVersionSummaryAsync(CancellationToken cancellationToken = default)
    {
        var lines = new List<string> { GetStatus() };

        if (File.Exists(MediaMtxPath))
            lines.Add("MediaMTX: " + FirstLine(await RunAsync(MediaMtxPath, ["--version"], TimeSpan.FromSeconds(8), cancellationToken)));
        if (TsAnalyzePath is { } ts)
            lines.Add("TSDuck: " + FirstLine(await RunAsync(ts, ["--version"], TimeSpan.FromSeconds(8), cancellationToken)));
        if (CcExtractorPath is { } cc)
            lines.Add("CCExtractor: " + FirstLine(await RunAsync(cc, ["--version"], TimeSpan.FromSeconds(8), cancellationToken)));
        if (File.Exists(WhisperPath))
            lines.Add("whisper.cpp: " + FirstLine(await RunAsync(WhisperPath, ["--help"], TimeSpan.FromSeconds(8), cancellationToken)));

        return string.Join(Environment.NewLine, lines);
    }

    private static string FirstLine(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "?";

    private static string? FindExecutable(string root, string name)
    {
        if (!Directory.Exists(root))
            return null;
        try
        {
            return Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    internal static async Task<string> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Não foi possível iniciar " + Path.GetFileName(executable));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var stdout = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException(Path.GetFileName(executable) + " excedeu o tempo limite.");
        }

        var output = (await stdout).Trim();
        var error = (await stderr).Trim();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{Path.GetFileName(executable)} retornou {process.ExitCode}: {error}");

        return string.IsNullOrWhiteSpace(output) ? error : output;
    }
}

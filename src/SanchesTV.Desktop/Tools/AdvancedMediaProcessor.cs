using System.Diagnostics;
using System.IO;

namespace SanchesTV.Desktop.Tools;

public sealed record MediaProcessingCapabilities(
    bool LoudNorm,
    bool SoXr,
    bool ZScale,
    bool Bwdif,
    bool LibVmaf,
    IReadOnlyList<string> H264Encoders)
{
    public string Summary => string.Join(Environment.NewLine,
        $"EBU R128/loudnorm: {(LoudNorm ? "sim" : "não")}",
        $"SoXR: {(SoXr ? "sim" : "não")}",
        $"zimg/zscale: {(ZScale ? "sim" : "não")}",
        $"BWDIF: {(Bwdif ? "sim" : "não")}",
        $"VMAF: {(LibVmaf ? "sim" : "não")}",
        $"H.264: {string.Join(", ", H264Encoders)}");
}

public sealed class AdvancedMediaProcessor
{
    private readonly MediaToolsService _tools;

    public AdvancedMediaProcessor(MediaToolsService tools) => _tools = tools;

    public async Task<MediaProcessingCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var filters = await MediaToolsService.RunAsync(
            _tools.FfmpegPath, ["-hide_banner", "-filters"], TimeSpan.FromSeconds(20), cancellationToken);
        var encoders = await MediaToolsService.RunAsync(
            _tools.FfmpegPath, ["-hide_banner", "-encoders"], TimeSpan.FromSeconds(20), cancellationToken);
        var build = await MediaToolsService.RunAsync(
            _tools.FfmpegPath, ["-hide_banner", "-buildconf"], TimeSpan.FromSeconds(20), cancellationToken);

        var h264 = new[] { "h264_nvenc", "h264_qsv", "h264_amf", "h264_mf", "libx264" }
            .Where(x => encoders.Contains(x, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new(
            filters.Contains("loudnorm", StringComparison.OrdinalIgnoreCase),
            build.Contains("libsoxr", StringComparison.OrdinalIgnoreCase),
            filters.Contains("zscale", StringComparison.OrdinalIgnoreCase),
            filters.Contains("bwdif", StringComparison.OrdinalIgnoreCase),
            filters.Contains("libvmaf", StringComparison.OrdinalIgnoreCase),
            h264);
    }

    public async Task<string> AnalyzeLoudnessAsync(string input, CancellationToken cancellationToken = default)
    {
        EnsureInput(input);
        return await MediaToolsService.RunAsync(
            _tools.FfmpegPath,
            ["-hide_banner", "-nostats", "-i", input,
             "-vn", "-af", "loudnorm=I=-16:LRA=11:TP=-1.5:print_format=json",
             "-f", "null", "NUL"],
            TimeSpan.FromMinutes(30),
            cancellationToken);
    }

    public async Task<string> NormalizeAudioAsync(string input, CancellationToken cancellationToken = default)
    {
        EnsureInput(input);
        var caps = await GetCapabilitiesAsync(cancellationToken);
        if (!caps.LoudNorm)
            throw new InvalidOperationException("O FFmpeg empacotado não possui o filtro loudnorm.");

        var output = OutputPath(input, "-ebu-r128", ".mkv");
        var filter = caps.SoXr
            ? "loudnorm=I=-16:LRA=11:TP=-1.5,aresample=resampler=soxr:precision=28"
            : "loudnorm=I=-16:LRA=11:TP=-1.5";

        await MediaToolsService.RunAsync(
            _tools.FfmpegPath,
            ["-hide_banner", "-y", "-i", input,
             "-map", "0:v?", "-map", "0:a?", "-map", "0:s?",
             "-c:v", "copy", "-c:s", "copy",
             "-af", filter, "-c:a", "aac", "-b:a", "192k",
             output],
            TimeSpan.FromHours(4),
            cancellationToken);

        return output;
    }

    public async Task<string> EnhanceVideoAsync(string input, CancellationToken cancellationToken = default)
    {
        EnsureInput(input);
        var caps = await GetCapabilitiesAsync(cancellationToken);
        var encoder = caps.H264Encoders.FirstOrDefault()
            ?? throw new InvalidOperationException("Nenhum encoder H.264 disponível.");

        var filters = new List<string>();
        if (caps.Bwdif)
            filters.Add("bwdif=mode=send_frame:parity=auto:deint=interlaced");
        if (caps.ZScale)
            filters.Add("zscale=filter=lanczos:dither=error_diffusion");
        else
            filters.Add("scale=iw:ih:flags=lanczos");

        var output = OutputPath(input, "-cinema", ".mkv");
        var args = new List<string>
        {
            "-hide_banner", "-y", "-i", input,
            "-map", "0:v:0?", "-map", "0:a?", "-map", "0:s?",
            "-vf", string.Join(",", filters),
            "-c:v", encoder
        };

        if (encoder == "libx264")
        {
            args.AddRange(["-preset", "medium", "-crf", "18"]);
        }
        else
        {
            args.AddRange(["-b:v", "8M"]);
        }

        args.AddRange(["-c:a", "copy", "-c:s", "copy", output]);
        await MediaToolsService.RunAsync(
            _tools.FfmpegPath, args, TimeSpan.FromHours(8), cancellationToken);
        return output;
    }

    public async Task<string> CompareVmafAsync(
        string reference,
        string distorted,
        CancellationToken cancellationToken = default)
    {
        EnsureInput(reference);
        EnsureInput(distorted);
        var caps = await GetCapabilitiesAsync(cancellationToken);
        if (!caps.LibVmaf)
            throw new InvalidOperationException("O FFmpeg empacotado não possui libvmaf.");

        return await MediaToolsService.RunAsync(
            _tools.FfmpegPath,
            ["-hide_banner", "-i", distorted, "-i", reference,
             "-lavfi", "libvmaf=log_fmt=json",
             "-f", "null", "NUL"],
            TimeSpan.FromHours(2),
            cancellationToken);
    }

    public async Task<string> CreateClipAsync(
        string input,
        TimeSpan start,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        EnsureInput(input);
        var output = OutputPath(input, $"-clip-{DateTime.Now:HHmmss}", ".mkv");
        await MediaToolsService.RunAsync(
            _tools.FfmpegPath,
            ["-hide_banner", "-y",
             "-ss", start.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
             "-i", input,
             "-t", duration.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
             "-map", "0", "-c", "copy", output],
            TimeSpan.FromMinutes(30),
            cancellationToken);
        return output;
    }

    private static void EnsureInput(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Arquivo de mídia não encontrado.", path);
    }

    private static string OutputPath(string input, string suffix, string extension)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "SanchesTV", "Processados");
        Directory.CreateDirectory(dir);
        var name = Path.GetFileNameWithoutExtension(input);
        return Path.Combine(dir, $"{name}{suffix}{extension}");
    }
}

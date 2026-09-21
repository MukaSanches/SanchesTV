namespace SanchesTV.Core.P2P;

public static class P2pMediaPolicy
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".m4v", ".avi", ".mov", ".webm", ".ts", ".m2ts",
        ".mts", ".mpg", ".mpeg", ".wmv", ".flv", ".ogv", ".3gp"
    };

    private static readonly HashSet<string> SubtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".ass", ".ssa", ".vtt", ".sub"
    };

    public static bool IsLikelyVideo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return VideoExtensions.Contains(Path.GetExtension(path));
    }

    public static bool IsLikelySubtitle(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return SubtitleExtensions.Contains(Path.GetExtension(path));
    }

    public static long CacheLimitBytes(int gigabytes)
    {
        var safe = Math.Clamp(gigabytes, 1, 500);
        return safe * 1024L * 1024L * 1024L;
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return bytes + " B";

        var units = new[] { "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = -1;

        do
        {
            value /= 1024d;
            unit++;
        }
        while (value >= 1024d && unit < units.Length - 1);

        return value.ToString("0.##") + " " + units[unit];
    }
}

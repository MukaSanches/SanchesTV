using System.Text.Json;
using SanchesTV.Core.P2P;

namespace SanchesTV.Desktop.P2P;

internal sealed class P2pCacheManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string Root { get; }
    public string SessionsRoot { get; }
    public string EngineCacheRoot { get; }
    public string SettingsPath { get; }

    public P2pCacheManager()
    {
        Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SanchesTV",
            "P2P");

        SessionsRoot = Path.Combine(Root, "sessions");
        EngineCacheRoot = Path.Combine(Root, "engine");
        SettingsPath = Path.Combine(Root, "settings.json");

        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(SessionsRoot);
        Directory.CreateDirectory(EngineCacheRoot);
    }

    public P2pSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new P2pSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<P2pSettings>(json, JsonOptions) ?? new P2pSettings();
        }
        catch
        {
            return new P2pSettings();
        }
    }

    public async Task SaveSettingsAsync(P2pSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Root);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json, cancellationToken);
    }

    public string CreateSessionDirectory()
    {
        var path = Path.Combine(SessionsRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(path);
        return path;
    }

    public long GetTotalCacheBytes()
    {
        try
        {
            return EnumerateFilesSafe(Root).Sum(f =>
            {
                try { return f.Length; } catch { return 0L; }
            });
        }
        catch
        {
            return 0;
        }
    }

    public Task PruneAsync(P2pSettings settings, string? activeSessionDirectory, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var limit = P2pMediaPolicy.CacheLimitBytes(settings.MaxCacheGb);
            var total = GetTotalCacheBytes();
            if (total <= limit)
                return;

            var sessionDirs = Directory.Exists(SessionsRoot)
                ? new DirectoryInfo(SessionsRoot)
                    .EnumerateDirectories()
                    .Where(d => !SamePath(d.FullName, activeSessionDirectory))
                    .OrderBy(d => d.LastWriteTimeUtc)
                    .ToArray()
                : Array.Empty<DirectoryInfo>();

            foreach (var dir in sessionDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (total <= limit)
                    break;

                var size = GetDirectoryBytes(dir.FullName);
                TryDeleteDirectory(dir.FullName);
                total = Math.Max(0, total - size);
            }
        }, cancellationToken);
    }

    public Task ClearInactiveSessionsAsync(string? activeSessionDirectory, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(SessionsRoot))
                return;

            foreach (var dir in Directory.EnumerateDirectories(SessionsRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (SamePath(dir, activeSessionDirectory))
                    continue;
                TryDeleteDirectory(dir);
            }
        }, cancellationToken);
    }

    public static long GetDirectoryBytes(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        return EnumerateFilesSafe(path).Sum(f =>
        {
            try { return f.Length; } catch { return 0L; }
        });
    }

    private static IEnumerable<FileInfo> EnumerateFilesSafe(string path)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(path));

        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            FileInfo[] files;
            DirectoryInfo[] dirs;

            try { files = dir.GetFiles(); } catch { files = Array.Empty<FileInfo>(); }
            try { dirs = dir.GetDirectories(); } catch { dirs = Array.Empty<DirectoryInfo>(); }

            foreach (var file in files)
                yield return file;
            foreach (var child in dirs)
                pending.Push(child);
        }
    }

    public static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static bool SamePath(string left, string? right)
    {
        if (string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

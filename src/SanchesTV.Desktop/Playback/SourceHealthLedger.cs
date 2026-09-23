using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SanchesTV.Core.Models;
using SanchesTV.Core.Playback;

namespace SanchesTV.Desktop.Playback;

public sealed class SourceHealthLedger
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly Dictionary<string, SourceHealthSnapshot> _entries;

    public SourceHealthLedger(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SanchesTV", "source-health-v8.json");
        _entries = Load(_path);
    }

    public IReadOnlyList<ChannelSource> Order(IEnumerable<ChannelSource> sources)
    {
        lock (_gate)
            return SourceHealthPolicy.Order(sources, ReadUnsafe, DateTimeOffset.UtcNow);
    }

    public void RecordSuccess(ChannelSource source, TimeSpan startup)
    {
        lock (_gate)
        {
            var current = ReadUnsafe(source) ?? new(0, 0, 0, 0, null, null);
            var ms = Math.Clamp(startup.TotalMilliseconds, 0, 120_000);
            _entries[Key(source)] = current with
            {
                Successes = current.Successes + 1,
                ConsecutiveFailures = 0,
                AverageStartupMs = current.AverageStartupMs <= 0 ? ms : current.AverageStartupMs * .72 + ms * .28,
                LastSuccessUtc = DateTimeOffset.UtcNow
            };
            PersistUnsafe();
        }
    }

    public void RecordFailure(ChannelSource source)
    {
        lock (_gate)
        {
            var current = ReadUnsafe(source) ?? new(0, 0, 0, 0, null, null);
            _entries[Key(source)] = current with
            {
                Failures = current.Failures + 1,
                ConsecutiveFailures = Math.Min(50, current.ConsecutiveFailures + 1),
                LastFailureUtc = DateTimeOffset.UtcNow
            };
            PersistUnsafe();
        }
    }

    public string Summary()
    {
        lock (_gate)
        {
            var attempts = _entries.Values.Sum(x => x.Successes + x.Failures);
            var successes = _entries.Values.Sum(x => x.Successes);
            var rate = attempts == 0 ? 0 : successes * 100d / attempts;
            var ttff = _entries.Values.Where(x => x.AverageStartupMs > 0)
                .Select(x => x.AverageStartupMs).DefaultIfEmpty(0).Average();
            return $"Playback Intelligence: {_entries.Count:N0} fontes • {rate:N0}% sucesso • TTFF {ttff:N0} ms";
        }
    }

    private SourceHealthSnapshot? ReadUnsafe(ChannelSource source) => _entries.GetValueOrDefault(Key(source));

    private static string Key(ChannelSource source)
    {
        var identity = $"{source.Provider}|{source.Url.Scheme}|{source.Url.Host}|{source.Url.AbsolutePath}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private static Dictionary<string, SourceHealthSnapshot> Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, SourceHealthSnapshot>>(File.ReadAllText(path))
                    ?? new(StringComparer.Ordinal)
                : new(StringComparer.Ordinal);
        }
        catch { return new(StringComparer.Ordinal); }
    }

    private void PersistUnsafe()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_entries));
            File.Move(temp, _path, true);
        }
        catch { }
    }
}

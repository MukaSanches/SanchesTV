using SanchesTV.Core.Models;

namespace SanchesTV.Core.Playback;

public sealed record SourceHealthSnapshot(
    int Successes,
    int Failures,
    int ConsecutiveFailures,
    double AverageStartupMs,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? LastFailureUtc);

public static class SourceHealthPolicy
{
    public static IReadOnlyList<ChannelSource> Order(
        IEnumerable<ChannelSource> sources,
        Func<ChannelSource, SourceHealthSnapshot?> history,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(history);

        return sources
            .OrderByDescending(source => Score(source, history(source), now))
            .ThenBy(source => source.Priority)
            .ToArray();
    }

    public static double Score(ChannelSource source, SourceHealthSnapshot? snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);

        double score = source.Status switch
        {
            StreamStatus.Online => 900,
            StreamStatus.NotTested => 420,
            StreamStatus.Unstable => 210,
            StreamStatus.Slow => 120,
            StreamStatus.Blocked => -450,
            StreamStatus.Offline => -650,
            StreamStatus.Error => -700,
            _ => 0
        };

        score -= Math.Clamp(source.Priority, -100, 10_000) * 2.5;
        if (source.Latency is { } latency)
            score -= Math.Min(260, latency.TotalMilliseconds / 18d);

        if (snapshot is null)
            return score;

        var attempts = snapshot.Successes + snapshot.Failures;
        if (attempts > 0)
            score += snapshot.Successes / (double)attempts * 520d;

        score -= Math.Clamp(snapshot.ConsecutiveFailures, 0, 50) * 95d;
        if (snapshot.AverageStartupMs > 0)
            score -= Math.Min(280, snapshot.AverageStartupMs / 30d);

        if (snapshot.LastSuccessUtc is { } successUtc)
        {
            var age = now - successUtc;
            if (age < TimeSpan.FromMinutes(30)) score += 180;
            else if (age < TimeSpan.FromHours(12)) score += 90;
            else if (age < TimeSpan.FromDays(7)) score += 35;
        }

        if (snapshot.LastFailureUtc is { } failureUtc &&
            now - failureUtc < TimeSpan.FromMinutes(10))
            score -= 140;

        return score;
    }
}

using SanchesTV.Core.Models;
using SanchesTV.Core.Playback;

namespace SanchesTV.Tests;

public sealed class SourceHealthPolicyTests
{
    private static ChannelSource Source(
        string provider,
        int priority = 0,
        StreamStatus status = StreamStatus.NotTested,
        int latencyMs = 0) =>
        new(
            Guid.NewGuid(),
            provider,
            new Uri($"https://{provider.ToLowerInvariant()}.example/live.m3u8"),
            priority,
            status,
            latencyMs > 0 ? TimeSpan.FromMilliseconds(latencyMs) : null);

    [Fact]
    public void Order_PrefersOnlineSourceOverOfflineSource()
    {
        var online = Source("Online", priority: 20, status: StreamStatus.Online);
        var offline = Source("Offline", priority: 0, status: StreamStatus.Offline);

        var ordered = SourceHealthPolicy.Order(
            [offline, online],
            _ => null,
            DateTimeOffset.Parse("2026-09-22T23:00:00-03:00"));

        Assert.Equal(online.Id, ordered[0].Id);
    }

    [Fact]
    public void Order_UsesHistoricalReliabilityBetweenEquivalentSources()
    {
        var reliable = Source("Reliable", priority: 5, status: StreamStatus.Online, latencyMs: 900);
        var flaky = Source("Flaky", priority: 5, status: StreamStatus.Online, latencyMs: 250);

        var now = DateTimeOffset.Parse("2026-09-22T23:00:00-03:00");
        var history = new Dictionary<Guid, SourceHealthSnapshot>
        {
            [reliable.Id] = new(
                Successes: 18,
                Failures: 1,
                ConsecutiveFailures: 0,
                AverageStartupMs: 700,
                LastSuccessUtc: now.AddMinutes(-3),
                LastFailureUtc: now.AddDays(-2)),
            [flaky.Id] = new(
                Successes: 2,
                Failures: 8,
                ConsecutiveFailures: 4,
                AverageStartupMs: 2800,
                LastSuccessUtc: now.AddHours(-4),
                LastFailureUtc: now.AddMinutes(-2))
        };

        var ordered = SourceHealthPolicy.Order(
            [flaky, reliable],
            source => history.GetValueOrDefault(source.Id),
            now);

        Assert.Equal(reliable.Id, ordered[0].Id);
    }

    [Fact]
    public void Score_PenalizesConsecutiveRecentFailures()
    {
        var source = Source("Primary", priority: 0, status: StreamStatus.Online, latencyMs: 400);
        var now = DateTimeOffset.Parse("2026-09-22T23:00:00-03:00");

        var healthy = SourceHealthPolicy.Score(
            source,
            new SourceHealthSnapshot(10, 1, 0, 500, now.AddMinutes(-5), now.AddDays(-1)),
            now);

        var failing = SourceHealthPolicy.Score(
            source,
            new SourceHealthSnapshot(10, 5, 5, 500, now.AddMinutes(-30), now.AddMinutes(-1)),
            now);

        Assert.True(healthy > failing);
    }
}

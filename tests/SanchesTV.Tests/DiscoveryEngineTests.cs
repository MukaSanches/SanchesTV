using SanchesTV.Core.Discovery;
using SanchesTV.Core.Models;
using Xunit;

namespace SanchesTV.Tests;

public sealed class DiscoveryEngineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T15:30:00Z");

    [Fact]
    public void Build_EmptyCatalogHasNoInventedAvailabilityOrSchedule()
    {
        var snapshot = new DiscoveryEngine().Build([], [], [], Now.ToOffset(TimeSpan.FromHours(-3)));

        Assert.Empty(snapshot.Favorites);
        Assert.Empty(snapshot.Recent);
        Assert.Empty(snapshot.Recommendations);
        Assert.Empty(snapshot.ScheduleByChannel);
        Assert.Equal(new DiscoveryMetrics(0, 0, 0, 0, 0, 0, 0, 0), snapshot.Metrics);
        Assert.Equal(TimeSpan.Zero, snapshot.GeneratedAtUtc.Offset);
        Assert.Equal(Now, snapshot.GeneratedAtUtc);
    }

    [Fact]
    public void Build_PreservesOfflineFavoritesAndRecentOrderWithoutDuplicates()
    {
        var favorite = Channel(1, "Favorite", favorite: true, status: StreamStatus.Offline);
        var missingSource = Channel(2, "No source") with { Sources = [] };
        var online = Channel(3, "Online", status: StreamStatus.Online);

        var snapshot = new DiscoveryEngine().Build(
            [favorite, missingSource, online],
            [missingSource.Id, Id(99), favorite.Id, missingSource.Id, online.Id], [], Now);

        Assert.Equal([favorite.Id], snapshot.Favorites.Select(channel => channel.Id));
        Assert.Equal([missingSource.Id, favorite.Id, online.Id], snapshot.Recent.Select(channel => channel.Id));
        Assert.DoesNotContain(snapshot.Recommendations, channel => channel.Id == favorite.Id);
        Assert.Equal(online.Id, snapshot.Recommendations[0].Id);
    }

    [Fact]
    public void Build_MergesRepeatedChannelIdsAndCountsUniqueSourcesHonestly()
    {
        var original = Channel(1, "Shared", status: StreamStatus.NotTested);
        var favoriteVersion = original with
        {
            IsFavorite = true,
            Sources = [Source(2, StreamStatus.Online), original.Sources[0]]
        };
        var other = Channel(3, "Other", status: StreamStatus.Slow);

        var snapshot = new DiscoveryEngine().Build(
            [original, favoriteVersion, other, original], [original.Id, original.Id], [], Now);

        Assert.Equal(2, snapshot.Metrics.ChannelCount);
        Assert.Equal(3, snapshot.Metrics.SourceCount);
        Assert.Equal(1, snapshot.Metrics.OnlineSourceCount);
        Assert.Equal(1, snapshot.Metrics.UntestedSourceCount);
        Assert.Equal(1, snapshot.Metrics.DegradedSourceCount);
        Assert.Equal(0, snapshot.Metrics.UnavailableSourceCount);
        Assert.Single(snapshot.Favorites);
        Assert.Equal(2, snapshot.Favorites[0].Sources.Count);
        Assert.Single(snapshot.Recent);
    }

    [Fact]
    public void Build_AllRailsHaveEightItemsButMetricsDescribeEntireCatalog()
    {
        var channels = Enumerable.Range(1, 30)
            .Select(index => Channel(index, $"Channel {index:00}", favorite: index <= 12))
            .ToArray();
        var history = channels.Reverse().Select(channel => channel.Id).ToArray();

        var snapshot = new DiscoveryEngine().Build(channels, history, [], Now);

        Assert.Equal(DiscoveryEngine.RailLimit, snapshot.Favorites.Count);
        Assert.Equal(DiscoveryEngine.RailLimit, snapshot.Recent.Count);
        Assert.Equal(DiscoveryEngine.RailLimit, snapshot.Recommendations.Count);
        Assert.Equal(30, snapshot.Metrics.ChannelCount);
        Assert.Equal(12, snapshot.Metrics.FavoriteCount);
        Assert.Equal(history.Take(8), snapshot.Recent.Select(channel => channel.Id));
        Assert.Equal(8, snapshot.Recommendations.Select(channel => channel.Id).Distinct().Count());
    }

    [Fact]
    public void Build_RecommendationsAndScheduleAreIndependentOfCatalogInputOrder()
    {
        var channels = Enumerable.Range(1, 18)
            .Select(index => Channel(index, $"Channel {index:00}",
                category: index % 2 == 0 ? "Films" : "News",
                country: index % 3 == 0 ? "BR" : "PT",
                language: index % 4 == 0 ? "en" : "pt",
                favorite: index == 7,
                status: index % 5 == 0 ? StreamStatus.Online : StreamStatus.NotTested) with { EpgId = "station" })
            .ToArray();
        var programs = new[]
        {
            Program("First", Now.AddMinutes(-30), Now.AddMinutes(30)),
            Program("Next", Now.AddMinutes(30), Now.AddMinutes(60)),
            Program("Later", Now.AddMinutes(60), Now.AddMinutes(90))
        };
        var engine = new DiscoveryEngine();

        var first = engine.Build(channels, [channels[4].Id, channels[2].Id], programs, Now);
        var reordered = engine.Build(channels.Reverse().ToArray(), [channels[4].Id, channels[2].Id], programs.Reverse().ToArray(), Now);

        Assert.Equal(first.Recommendations.Select(channel => channel.Id), reordered.Recommendations.Select(channel => channel.Id));
        Assert.Equal(first.Favorites.Select(channel => channel.Id), reordered.Favorites.Select(channel => channel.Id));
        Assert.Equal(first.Metrics, reordered.Metrics);
        Assert.All(channels, channel => Assert.Equal(first.ScheduleByChannel[channel.Id], reordered.ScheduleByChannel[channel.Id]));
    }

    [Fact]
    public void Build_RecordedHealthTakesPrecedenceOverAffinityAndUntestedIsNotOnline()
    {
        var favorite = Channel(1, "Favorite", category: "Films", favorite: true, status: StreamStatus.Offline);
        var offlineMatch = Channel(2, "A offline match", category: "Films", status: StreamStatus.Offline);
        var unknown = Channel(3, "Unknown", category: "News", status: StreamStatus.NotTested);
        var online = Channel(4, "Z online", category: "Kids", status: StreamStatus.Online);
        var degraded = Channel(5, "Degraded", category: "Films", status: StreamStatus.Unstable);

        var snapshot = new DiscoveryEngine().Build([favorite, offlineMatch, unknown, online, degraded], [], [], Now);

        Assert.Equal([online.Id, unknown.Id, degraded.Id, offlineMatch.Id], snapshot.Recommendations.Select(channel => channel.Id));
        Assert.Equal(1, snapshot.Metrics.OnlineSourceCount);
        Assert.Equal(1, snapshot.Metrics.UntestedSourceCount);
        Assert.Equal(1, snapshot.Metrics.DegradedSourceCount);
        Assert.Equal(2, snapshot.Metrics.UnavailableSourceCount);
    }

    [Fact]
    public void Build_DiversifiesCategoryCountryAndLanguageAfterAnAffinityPick()
    {
        var favorite = Channel(1, "Favorite", category: "Science", country: "US", language: "en", favorite: true);
        var match = Channel(2, "A match", category: "science ", country: "us", language: "EN");
        var repeat = Channel(3, "B repeat", category: "Science", country: "US", language: "en");
        var kids = Channel(4, "C kids", category: "Kids", country: "BR", language: "pt");
        var sports = Channel(5, "D sports", category: "Sports", country: "ES", language: "es");

        var snapshot = new DiscoveryEngine().Build([favorite, repeat, sports, kids, match], [], [], Now);

        Assert.Equal([match.Id, kids.Id, sports.Id, repeat.Id], snapshot.Recommendations.Select(channel => channel.Id));
    }

    [Fact]
    public void Build_RecentInterestsInfluenceDiscoveryWithoutNeedingFavorites()
    {
        var recent = Channel(1, "Z recent", category: "Documentary", country: "BR", language: "pt");
        var related = Channel(2, "B related", category: " Documentary ", country: "br", language: "PT");
        var other = Channel(3, "A other", category: "Music", country: "US", language: "en");

        var snapshot = new DiscoveryEngine().Build([recent, related, other], [recent.Id], [], Now);

        Assert.Equal(related.Id, snapshot.Recommendations[0].Id);
    }

    [Fact]
    public void Build_EpgMatchesUtcInstantsAndCalculatesRealProgress()
    {
        var channel = Channel(1, "News") with { EpgId = "station" };
        var current = Program("Current", DateTimeOffset.Parse("2026-09-30T12:00:00-03:00"), DateTimeOffset.Parse("2026-09-30T13:00:00-03:00"));
        var next = Program("Next", DateTimeOffset.Parse("2026-09-30T18:00:00+02:00"), DateTimeOffset.Parse("2026-09-30T19:00:00+02:00"));

        var snapshot = new DiscoveryEngine().Build([channel], [], [next, current], Now);
        var schedule = snapshot.ScheduleByChannel[channel.Id];

        Assert.Equal(current, schedule.Now);
        Assert.Equal(next, schedule.Next);
        Assert.Equal(0.5, schedule.Progress, precision: 8);
        Assert.Equal(1, snapshot.Metrics.EpgChannelCount);
    }

    [Fact]
    public void Build_EpgIncludesStartAndExcludesEndAtExactBoundary()
    {
        var channel = Channel(1, "News") with { EpgId = "station" };
        var ended = Program("Ended", Now.AddHours(-1), Now);
        var starting = Program("Starting", Now, Now.AddHours(1));
        var next = Program("Next", Now.AddHours(1), Now.AddHours(2));

        var snapshot = new DiscoveryEngine().Build([channel], [], [ended, starting, next], Now);
        var schedule = snapshot.ScheduleByChannel[channel.Id];

        Assert.Equal(starting, schedule.Now);
        Assert.Equal(next, schedule.Next);
        Assert.Equal(0, schedule.Progress);
    }

    [Fact]
    public void Build_EpgRejectsInvalidIntervalsAndUsesLatestStartForOverlaps()
    {
        var channel = Channel(1, "News") with { EpgId = "station" };
        var broad = Program("Broad", Now.AddHours(-1), Now.AddHours(2));
        var latest = Program("Latest", Now.AddMinutes(-10), Now.AddMinutes(10));
        var invalid = Program("Invalid", Now, Now.AddMinutes(-1));
        var zeroLength = Program("Zero", Now.AddMinutes(1), Now.AddMinutes(1));
        var future = Program("Future", Now.AddMinutes(20), Now.AddMinutes(40));

        var snapshot = new DiscoveryEngine().Build([channel], [], [invalid, broad, zeroLength, latest, future], Now);

        Assert.Equal(latest, snapshot.ScheduleByChannel[channel.Id].Now);
        Assert.Equal(future, snapshot.ScheduleByChannel[channel.Id].Next);
        Assert.Equal(0.5, snapshot.ScheduleByChannel[channel.Id].Progress, precision: 8);
    }

    [Fact]
    public void Build_EpgDoesNotInventNowOrMatchADifferentChannelId()
    {
        var channel = Channel(1, "News") with { EpgId = "station" };
        var future = Program("Future", Now.AddHours(1), Now.AddHours(2));
        var differentChannel = Program("Other", Now.AddMinutes(-10), Now.AddMinutes(10)) with { ChannelEpgId = "STATION" };

        var snapshot = new DiscoveryEngine().Build([channel], [], [future, differentChannel], Now);
        var schedule = snapshot.ScheduleByChannel[channel.Id];

        Assert.Null(schedule.Now);
        Assert.Equal(future, schedule.Next);
        Assert.Equal(0, schedule.Progress);
    }

    private static Guid Id(int number) => new(number, 0, 0, new byte[8]);

    private static ChannelSource Source(int number, StreamStatus status) =>
        new(Id(number), "Test source", new Uri($"https://example.com/{number}/live.m3u8"), 0, status);

    private static Channel Channel(
        int number,
        string name,
        string category = "News",
        string country = "BR",
        string language = "pt",
        bool favorite = false,
        StreamStatus status = StreamStatus.NotTested) =>
        new(Id(number), name, name.ToUpperInvariant(), country, language, null, null, category,
            null, null, null, [Source(number, status)], favorite);

    private static EpgProgram Program(string title, DateTimeOffset start, DateTimeOffset end) =>
        new("station", start, end, title, null, null);
}

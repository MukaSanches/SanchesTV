using SanchesTV.Core.Models;

namespace SanchesTV.Core.Discovery;

/// <summary>A local, reproducible view of the existing catalog. No network probing is performed.</summary>
public sealed record DiscoverySnapshot(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<Channel> Favorites,
    IReadOnlyList<Channel> Recent,
    IReadOnlyList<Channel> Recommendations,
    IReadOnlyDictionary<Guid, DiscoverySchedule> ScheduleByChannel,
    DiscoveryMetrics Metrics);

/// <summary>EPG intervals include their start and exclude their end. Progress is between zero and one.</summary>
public sealed record DiscoverySchedule(EpgProgram? Now, EpgProgram? Next, double Progress);

/// <summary>
/// Counts describe the supplied catalog and its recorded source statuses, not a live availability guarantee.
/// Untested sources remain separate from online sources. Degraded means Slow or Unstable;
/// unavailable means Offline, Error or Blocked. Channels without sources are still counted.
/// </summary>
public sealed record DiscoveryMetrics(
    int ChannelCount,
    int SourceCount,
    int OnlineSourceCount,
    int UntestedSourceCount,
    int DegradedSourceCount,
    int UnavailableSourceCount,
    int FavoriteCount,
    int EpgChannelCount);

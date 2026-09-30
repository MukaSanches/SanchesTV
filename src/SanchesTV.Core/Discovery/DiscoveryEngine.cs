using System.Collections.ObjectModel;
using System.Text;
using SanchesTV.Core.Models;

namespace SanchesTV.Core.Discovery;

/// <summary>
/// Builds small discovery rails from catalog metadata, favorites, history and imported EPG.
/// Recommendations use local affinity and variety, with recorded source status taking precedence.
/// </summary>
public sealed class DiscoveryEngine
{
    public const int RailLimit = 8;

    public DiscoverySnapshot Build(
        IReadOnlyList<Channel> channels,
        IReadOnlyList<Guid> recentChannelIds,
        IReadOnlyList<EpgProgram> programs,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(recentChannelIds);
        ArgumentNullException.ThrowIfNull(programs);

        var catalog = CanonicalCatalog(channels);
        var byId = catalog.ToDictionary(channel => channel.Id);
        var allFavorites = catalog.Where(channel => channel.IsFavorite).ToArray();
        var allRecent = recentChannelIds.Distinct()
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToArray();

        // Source health never removes a user's saved channels or playback history.
        var favorites = allFavorites
            .OrderBy(channel => channel.MyTvPosition ?? int.MaxValue)
            .ThenBy(channel => channel.VirtualNumber ?? int.MaxValue)
            .ThenBy(channel => Key(channel.Name), StringComparer.Ordinal)
            .ThenBy(channel => channel.Id)
            .Take(RailLimit)
            .ToArray();
        var recent = allRecent.Take(RailLimit).ToArray();
        var recommendations = Recommend(catalog, allFavorites, allRecent);

        var utcNow = now.ToUniversalTime();
        var schedules = BuildSchedules(catalog, programs, utcNow);
        // A shared source ID represents one source, including when the input repeats a channel.
        var sources = catalog.SelectMany(channel => channel.Sources)
            .GroupBy(source => source.Id)
            .Select(group => CanonicalSource(group))
            .ToArray();
        var metrics = new DiscoveryMetrics(
            catalog.Length,
            sources.Length,
            sources.Count(source => source.Status == StreamStatus.Online),
            sources.Count(source => source.Status == StreamStatus.NotTested),
            sources.Count(source => source.Status is StreamStatus.Slow or StreamStatus.Unstable),
            sources.Count(source => source.Status is StreamStatus.Offline or StreamStatus.Error or StreamStatus.Blocked),
            allFavorites.Length,
            schedules.Values.Count(schedule => schedule.Now is not null || schedule.Next is not null));

        return new DiscoverySnapshot(
            utcNow,
            Array.AsReadOnly(favorites),
            Array.AsReadOnly(recent),
            Array.AsReadOnly(recommendations),
            new ReadOnlyDictionary<Guid, DiscoverySchedule>(schedules),
            metrics);
    }

    private static Channel[] CanonicalCatalog(IReadOnlyList<Channel> channels) => channels
        .GroupBy(channel => channel.Id)
        .Select(group =>
        {
            // Preserve favorite state and every distinct source when catalog input contains duplicate IDs.
            var representative = group
                .OrderByDescending(channel => channel.IsFavorite)
                .ThenBy(channel => channel.MyTvPosition ?? int.MaxValue)
                .ThenBy(channel => channel.VirtualNumber ?? int.MaxValue)
                .ThenBy(channel => Key(channel.Name), StringComparer.Ordinal)
                .ThenBy(channel => channel.Name, StringComparer.Ordinal)
                .ThenBy(channel => channel.NormalizedName, StringComparer.Ordinal)
                .ThenBy(channel => channel.Category, StringComparer.Ordinal)
                .ThenBy(channel => channel.Country, StringComparer.Ordinal)
                .ThenBy(channel => channel.Language, StringComparer.Ordinal)
                .ThenBy(channel => channel.EpgId, StringComparer.Ordinal)
                .ThenBy(channel => channel.State, StringComparer.Ordinal)
                .ThenBy(channel => channel.Region, StringComparer.Ordinal)
                .ThenBy(channel => channel.Logo, StringComparer.Ordinal)
                .First();
            var sources = group.SelectMany(channel => channel.Sources)
                .GroupBy(source => source.Id)
                .Select(CanonicalSource)
                .OrderBy(source => source.Priority)
                .ThenBy(source => source.Id)
                .ToArray();
            return representative with { Sources = Array.AsReadOnly(sources) };
        })
        .OrderBy(channel => Key(channel.Name), StringComparer.Ordinal)
        .ThenBy(channel => channel.Id)
        .ToArray();

    private static ChannelSource CanonicalSource(IEnumerable<ChannelSource> sources) => sources
        // Conflicting records have no observation timestamp. Prefer the cautious status deterministically.
        .OrderBy(source => StatusTier(source.Status))
        .ThenBy(source => source.Status)
        .ThenBy(source => source.Priority)
        .ThenBy(source => source.Url.ToString(), StringComparer.Ordinal)
        .ThenBy(source => source.Provider, StringComparer.Ordinal)
        .ThenBy(source => source.Latency)
        .ThenBy(source => source.Resolution, StringComparer.Ordinal)
        .ThenBy(source => source.VideoCodec, StringComparer.Ordinal)
        .ThenBy(source => source.AudioCodec, StringComparer.Ordinal)
        .ThenBy(source => source.Bitrate)
        .ThenBy(source => source.LastError, StringComparer.Ordinal)
        .ThenBy(source => source.UserAgent, StringComparer.Ordinal)
        .ThenBy(source => source.Referrer, StringComparer.Ordinal)
        .ThenBy(source => source.Origin, StringComparer.Ordinal)
        .First();

    private static Channel[] Recommend(Channel[] catalog, Channel[] favorites, Channel[] recent)
    {
        var preference = favorites.Select(channel => (Channel: channel, Weight: 2d))
            .Concat(recent.Select(channel => (Channel: channel, Weight: 1d)))
            .ToArray();
        var favoriteCategories = Preference(preference, channel => channel.Category);
        var favoriteCountries = Preference(preference, channel => channel.Country);
        var favoriteLanguages = Preference(preference, channel => channel.Language);
        // Favorite channels already have their own rail; history still helps recommend familiar interests.
        // Precompute affinity once so a large imported catalog does not need a history scan for every pick.
        var candidates = catalog.Where(channel => !channel.IsFavorite)
            .Select(channel => new RecommendationCandidate(
                channel,
                ChannelTier(channel),
                Key(channel.Name),
                Key(channel.Category),
                Key(channel.Country),
                Key(channel.Language),
                20 * favoriteCategories.GetValueOrDefault(Key(channel.Category)) +
                12 * favoriteCountries.GetValueOrDefault(Key(channel.Country)) +
                8 * favoriteLanguages.GetValueOrDefault(Key(channel.Language))))
            .ToList();
        var selected = new List<Channel>(RailLimit);
        var categories = new Dictionary<string, int>(StringComparer.Ordinal);
        var countries = new Dictionary<string, int>(StringComparer.Ordinal);
        var languages = new Dictionary<string, int>(StringComparer.Ordinal);

        while (selected.Count < RailLimit && candidates.Count > 0)
        {
            var pick = candidates
                .OrderByDescending(candidate => candidate.HealthTier)
                .ThenByDescending(candidate =>
                    Variety(candidate.Category, categories, 45) +
                    Variety(candidate.Country, countries, 25) +
                    Variety(candidate.Language, languages, 15) + candidate.Affinity)
                .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Channel.Id)
                .First();
            selected.Add(pick.Channel);
            candidates.Remove(pick);
            Count(pick.Category, categories);
            Count(pick.Country, countries);
            Count(pick.Language, languages);
        }

        return selected.ToArray();
    }

    private static double Variety(string key, Dictionary<string, int> counts, double weight) =>
        key.Length == 0 ? 0 : weight / (1 + counts.GetValueOrDefault(key));

    private static Dictionary<string, double> Preference(
        (Channel Channel, double Weight)[] preference,
        Func<Channel, string?> field)
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var item in preference)
        {
            var key = Key(field(item.Channel));
            if (key.Length > 0)
                weights[key] = weights.GetValueOrDefault(key) + item.Weight;
        }
        var total = weights.Values.Sum();
        return weights.ToDictionary(item => item.Key, item => item.Value / total, StringComparer.Ordinal);
    }

    private static void Count(string key, Dictionary<string, int> counts)
    {
        if (key.Length > 0)
            counts[key] = counts.GetValueOrDefault(key) + 1;
    }

    private sealed record RecommendationCandidate(
        Channel Channel, int HealthTier, string Name, string Category, string Country, string Language, double Affinity);

    private static int ChannelTier(Channel channel) => channel.Sources.Count == 0
        ? -1
        : channel.Sources.Max(source => StatusTier(source.Status));

    private static int StatusTier(StreamStatus status) => status switch
    {
        StreamStatus.Online => 3,
        StreamStatus.NotTested => 2,
        StreamStatus.Slow or StreamStatus.Unstable => 1,
        _ => 0
    };

    private static Dictionary<Guid, DiscoverySchedule> BuildSchedules(
        Channel[] catalog, IReadOnlyList<EpgProgram> programs, DateTimeOffset now)
    {
        var byEpgId = programs
            .Where(program => !string.IsNullOrWhiteSpace(program.ChannelEpgId) && program.End > program.Start)
            .GroupBy(program => program.ChannelEpgId.Trim(), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Distinct()
                    .OrderBy(program => program.Start.UtcTicks)
                    .ThenBy(program => program.End.UtcTicks)
                    .ThenBy(program => program.Title, StringComparer.Ordinal)
                    .ThenBy(program => program.Description, StringComparer.Ordinal)
                    .ThenBy(program => program.Category, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        var schedules = new Dictionary<Guid, DiscoverySchedule>();

        foreach (var channel in catalog)
        {
            EpgProgram? current = null;
            EpgProgram? next = null;
            if (!string.IsNullOrWhiteSpace(channel.EpgId) && byEpgId.TryGetValue(channel.EpgId.Trim(), out var schedule))
            {
                // If imported programs overlap, the most recently started program wins consistently.
                current = schedule.Where(program => program.Start <= now && now < program.End)
                    .OrderByDescending(program => program.Start.UtcTicks)
                    .ThenBy(program => program.End.UtcTicks)
                    .ThenBy(program => program.Title, StringComparer.Ordinal)
                    .ThenBy(program => program.Description, StringComparer.Ordinal)
                    .ThenBy(program => program.Category, StringComparer.Ordinal)
                    .FirstOrDefault();
                next = schedule.FirstOrDefault(program => program.Start > now);
            }

            var progress = current is null ? 0 :
                Math.Clamp((now - current.Start).TotalSeconds / (current.End - current.Start).TotalSeconds, 0, 1);
            schedules[channel.Id] = new DiscoverySchedule(current, next, progress);
        }

        return schedules;
    }

    private static string Key(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
}

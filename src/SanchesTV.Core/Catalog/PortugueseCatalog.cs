using Polly;
using Polly.Retry;
using SanchesTV.Core.Models;
using SanchesTV.Core.Parsing;

namespace SanchesTV.Core.Catalog;

public enum PortugueseCatalogFilter
{
    All,
    AllEntries,
    BrazilPublicOnly,
    M3uPtTelevision,
    LusophoneCountryGroup
}

public sealed record PortugueseCatalogSource(
    string Name,
    string Repository,
    Uri PlaylistUrl,
    PortugueseCatalogFilter Filter,
    string? DefaultCountry = null,
    string? DefaultLanguage = "Portuguese",
    string? DefaultCategory = null);

public sealed record PortugueseCatalogSyncResult(
    int DownloadedSources,
    int FailedSources,
    int CandidateChannels,
    IReadOnlyList<Channel> Channels,
    IReadOnlyList<string> Errors);

public static class PortugueseCatalogRegistry
{
    public static IReadOnlyList<PortugueseCatalogSource> Sources { get; } =
    [
        new("IPTV-org Brasil", "iptv-org/iptv",
            new Uri("https://iptv-org.github.io/iptv/countries/br.m3u"),
            PortugueseCatalogFilter.AllEntries, "BR", "Portuguese"),
        new("IPTV-org Português", "iptv-org/iptv",
            new Uri("https://iptv-org.github.io/iptv/languages/por.m3u"),
            PortugueseCatalogFilter.AllEntries, null, "Portuguese"),
        new("IPTV-org Portugal", "iptv-org/iptv",
            new Uri("https://iptv-org.github.io/iptv/countries/pt.m3u"),
            PortugueseCatalogFilter.AllEntries, "PT", "Portuguese"),
        new("IPTV-org Filmes", "iptv-org/iptv",
            new Uri("https://iptv-org.github.io/iptv/categories/movies.m3u"),
            PortugueseCatalogFilter.AllEntries, null, null, "Movies"),
        new("Brasil Full — abertos/públicos", "iptv-com/iptv",
            new Uri("https://github.com/iptv-com/iptv/raw/refs/heads/main/lists/brazil.m3u"),
            PortugueseCatalogFilter.BrazilPublicOnly, "BR", "Portuguese"),
        new("FTA IPTV Brasil", "joaoguidugli/FTA-IPTV-Brasil",
            new Uri("https://raw.githubusercontent.com/joaoguidugli/FTA-IPTV-Brasil/master/playlist.m3u8"),
            PortugueseCatalogFilter.All, "BR", "Portuguese"),
        new("M3UPT Portugal/Lusofonia", "LITUATUI/M3UPT",
            new Uri("https://raw.githubusercontent.com/LITUATUI/M3UPT/main/M3U/M3UPT.m3u"),
            PortugueseCatalogFilter.M3uPtTelevision, null, "Portuguese"),
        new("Free-TV Brasil/Portugal", "Free-TV/IPTV",
            new Uri("https://raw.githubusercontent.com/Free-TV/IPTV/master/playlist.m3u8"),
            PortugueseCatalogFilter.LusophoneCountryGroup, null, "Portuguese")
    ];
}

public static class PortugueseCatalogRules
{
    private static readonly HashSet<string> CountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BR", "PT", "AO", "MZ", "CV", "GW", "ST", "TL"
    };

    private static readonly HashSet<string> CountryGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "Brazil", "Brasil", "Portugal", "Angola", "Mozambique", "Moçambique",
        "Cape Verde", "Cabo Verde", "Guinea-Bissau", "Guiné-Bissau",
        "Sao Tome and Principe", "São Tomé e Príncipe", "East Timor", "Timor-Leste"
    };

    public static bool IsIncluded(Channel channel, PortugueseCatalogFilter filter)
    {
        return filter switch
        {
            PortugueseCatalogFilter.AllEntries => true,
            PortugueseCatalogFilter.BrazilPublicOnly => IsBrazilPublicChannel(channel),
            PortugueseCatalogFilter.All => IsPortugueseMetadata(channel),
            PortugueseCatalogFilter.M3uPtTelevision =>
                string.Equals(channel.Category, "TV", StringComparison.OrdinalIgnoreCase) &&
                IsLusophone(channel),
            PortugueseCatalogFilter.LusophoneCountryGroup =>
                CountryGroups.Contains(channel.Category ?? string.Empty) || IsLusophoneCountry(channel),
            _ => false
        };
    }

    public static Channel ApplyDefaults(Channel channel, PortugueseCatalogSource source)
    {
        var country = channel.Country ?? source.DefaultCountry;
        var language = channel.Language ?? source.DefaultLanguage;

        if (source.Filter == PortugueseCatalogFilter.LusophoneCountryGroup && string.IsNullOrWhiteSpace(country))
            country = GroupToCountryCode(channel.Category);

        return channel with
        {
            Country = country,
            Language = language,
            Category = source.DefaultCategory ?? channel.Category,
            Sources = channel.Sources.Select(s => s with
            {
                Provider = $"GitHub · {source.Repository} · {source.Name}"
            }).ToArray()
        };
    }


    private static readonly string[] PremiumNameFragments =
    [
        "espn", "sportv", "telecine", "hbo", "tnt", "warner", "discovery",
        "history", "a&e", "axn", "sony channel", "sony movies", "paramount",
        "nickelodeon", "nick jr", "cartoon network", "cartoonito", "globonews",
        "gnt", "multishow", "premiere", "combate", "universal tv",
        "studio universal", "megapix", "cinemax", "tlc", "food network",
        "hgtv", "lifetime", "adult swim", "mtv live"
    ];

    private static bool IsBrazilPublicChannel(Channel channel)
    {
        var name = TextNormalizer.Normalize(channel.Name);
        return !PremiumNameFragments.Any(fragment =>
            name.Contains(TextNormalizer.Normalize(fragment), StringComparison.Ordinal));
    }

    private static bool IsPortugueseMetadata(Channel channel)
    {
        if (IsLusophoneCountry(channel))
            return true;
        var language = TextNormalizer.Normalize(channel.Language ?? string.Empty);
        return language.Contains("portugu", StringComparison.Ordinal);
    }

    private static bool IsLusophone(Channel channel)
    {
        if (IsLusophoneCountry(channel))
            return true;

        var id = channel.EpgId ?? string.Empty;
        var dot = id.LastIndexOf('.');
        if (dot >= 0 && dot < id.Length - 1)
        {
            var code = id[(dot + 1)..].Split('@')[0];
            if (CountryCodes.Contains(code))
                return true;
        }

        var normalized = TextNormalizer.Normalize(channel.Name);
        return normalized.Contains("portugues", StringComparison.Ordinal) ||
               normalized.Contains("portugal", StringComparison.Ordinal) ||
               normalized.Contains("brasil", StringComparison.Ordinal);
    }

    private static bool IsLusophoneCountry(Channel channel) =>
        !string.IsNullOrWhiteSpace(channel.Country) && CountryCodes.Contains(channel.Country);

    private static string? GroupToCountryCode(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return null;

        return group.Trim() switch
        {
            "Brazil" or "Brasil" => "BR",
            "Portugal" => "PT",
            "Angola" => "AO",
            "Mozambique" or "Moçambique" => "MZ",
            "Cape Verde" or "Cabo Verde" => "CV",
            "Guinea-Bissau" or "Guiné-Bissau" => "GW",
            "Sao Tome and Principe" or "São Tomé e Príncipe" => "ST",
            "East Timor" or "Timor-Leste" => "TL",
            _ => null
        };
    }
}

public sealed class PortugueseCatalogSyncService(HttpClient httpClient)
{
    private readonly ResiliencePipeline<string> _downloadPipeline =
        new ResiliencePipelineBuilder<string>()
            .AddRetry(new RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(450),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<string>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
            })
            .AddTimeout(TimeSpan.FromSeconds(25))
            .Build();

    public async Task<PortugueseCatalogSyncResult> DownloadAsync(CancellationToken cancellationToken = default)
    {
        var channels = new List<Channel>();
        var errors = new List<string>();
        var succeeded = 0;

        foreach (var source in PortugueseCatalogRegistry.Sources)
        {
            try
            {
                var text = await _downloadPipeline.ExecuteAsync(async token =>
                {
                    using var response = await httpClient.GetAsync(source.PlaylistUrl, token);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(token);
                }, cancellationToken);

                var parsed = M3uParser.Parse(text, source.Name)
                    .Where(c => PortugueseCatalogRules.IsIncluded(c, source.Filter))
                    .Select(c => PortugueseCatalogRules.ApplyDefaults(c, source))
                    .Where(c => c.Sources.Any(s => IsSupportedUri(s.Url)))
                    .ToArray();

                channels.AddRange(parsed);
                succeeded++;
            }
            catch (Exception ex)
            {
                errors.Add($"{source.Name}: {ex.Message}");
            }
        }

        return new PortugueseCatalogSyncResult(
            succeeded, errors.Count, channels.Count, channels, errors);
    }

    private static bool IsSupportedUri(Uri uri) =>
        uri.Scheme is "http" or "https" or "rtsp";
}

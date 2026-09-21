using System.Text.RegularExpressions;
using SanchesTV.Core.Models;

namespace SanchesTV.Core.Parsing;

public static class M3uParser
{
    private static readonly Regex AttributeRegex = new(@"(?<key>[\w-]+)=""(?<value>[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex EpgSuffixRegex = new(@"(@(?:SD|HD|FHD|4K)|\(m3u4u\))$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<Channel> Parse(string text, string provider = "M3U")
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var channels = new List<Channel>();
        string? info = null;
        string? userAgent = null;
        string? referrer = null;
        string? origin = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                info = line;
                userAgent = null;
                referrer = null;
                origin = null;
                continue;
            }

            if (info is not null && line.StartsWith("#EXTVLCOPT:", StringComparison.OrdinalIgnoreCase))
            {
                var option = line["#EXTVLCOPT:".Length..];
                var equals = option.IndexOf('=');
                if (equals > 0)
                {
                    var key = option[..equals].Trim();
                    var value = option[(equals + 1)..].Trim().Trim('"');

                    if (key.Equals("http-user-agent", StringComparison.OrdinalIgnoreCase))
                        userAgent = value;
                    else if (key.Equals("http-referrer", StringComparison.OrdinalIgnoreCase))
                        referrer = value;
                    else if (key.Equals("http-origin", StringComparison.OrdinalIgnoreCase))
                        origin = value;
                }

                continue;
            }

            if (line.StartsWith("#"))
                continue;

            if (info is null || !Uri.TryCreate(line, UriKind.Absolute, out var uri))
                continue;

            var attributes = AttributeRegex.Matches(info)
                .ToDictionary(
                    m => m.Groups["key"].Value,
                    m => m.Groups["value"].Value,
                    StringComparer.OrdinalIgnoreCase);

            var comma = info.LastIndexOf(',');
            var displayName = comma >= 0 ? info[(comma + 1)..].Trim() : "Canal";

            if (attributes.TryGetValue("tvg-name", out var tvgName) &&
                !string.IsNullOrWhiteSpace(tvgName))
            {
                displayName = tvgName.Trim();
            }

            var epgId = CanonicalizeEpgId(attributes.GetValueOrDefault("tvg-id"));
            var country = NormalizeCountry(attributes.GetValueOrDefault("tvg-country"));
            var language = NormalizeLanguage(attributes.GetValueOrDefault("tvg-language"));

            var source = new ChannelSource(
                Guid.NewGuid(),
                provider,
                uri,
                0,
                UserAgent: userAgent,
                Referrer: referrer,
                Origin: origin);

            channels.Add(new Channel(
                Guid.NewGuid(),
                displayName,
                TextNormalizer.Normalize(displayName),
                country,
                language,
                null,
                null,
                attributes.GetValueOrDefault("group-title"),
                attributes.GetValueOrDefault("tvg-logo"),
                epgId,
                null,
                new[] { source }));

            info = null;
            userAgent = null;
            referrer = null;
            origin = null;
        }

        return channels;
    }

    public static string? CanonicalizeEpgId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = EpgSuffixRegex.Replace(value.Trim(), string.Empty);
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static string? NormalizeCountry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var first = value
            .Split(';', ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(first) ? null : first.ToUpperInvariant();
    }

    private static string? NormalizeLanguage(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

using System.Text.RegularExpressions;
using SanchesTV.Core.Models;

namespace SanchesTV.Core.Parsing;

public static class M3uParser
{
    private static readonly Regex AttributeRegex = new(@"(?<key>[\w-]+)=""(?<value>[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex EpgSuffixRegex = new(@"(@(?:SD|HD|FHD|4K)|\(m3u4u\))$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<Channel> Parse(string text, string provider = "M3U")
    {
        ArgumentNullException.ThrowIfNull(text);
        using var input = new StringReader(text);

        var channels = new List<Channel>();
        string? info = null;
        string? userAgent = null;
        string? referrer = null;
        string? origin = null;

        string? raw;
        while ((raw = input.ReadLine()) is not null)
        {
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0) continue;
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

            if (info is null || !Uri.TryCreate(line, UriKind.Absolute, out var uri) ||
                uri.IsFile || uri.Scheme is "data" or "javascript")
            {
                info = userAgent = referrer = origin = null;
                continue;
            }

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in AttributeRegex.Matches(info))
                attributes[match.Groups["key"].Value] = match.Groups["value"].Value;

            var comma = FindMetadataSeparator(info);
            var displayName = comma >= 0 ? info[(comma + 1)..].Trim() : "Canal";

            if (attributes.TryGetValue("tvg-name", out var tvgName) &&
                !string.IsNullOrWhiteSpace(tvgName))
            {
                displayName = tvgName.Trim();
            }

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Canal";
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

    private static int FindMetadataSeparator(string info)
    {
        bool inQuotes = false;
        for (var i = 0; i < info.Length; i++)
        {
            if (info[i] == '"') inQuotes = !inQuotes;
            else if (info[i] == ',' && !inQuotes) return i;
        }
        return -1;
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

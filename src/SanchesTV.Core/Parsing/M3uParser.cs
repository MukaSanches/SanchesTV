using System.Text.RegularExpressions;
using SanchesTV.Core.Models;

namespace SanchesTV.Core.Parsing;

public static class M3uParser
{
    private static readonly Regex AttributeRegex = new(@"(?<key>[\w-]+)=""(?<value>[^""]*)""", RegexOptions.Compiled);

    public static IReadOnlyList<Channel> Parse(string text, string provider = "M3U")
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var channels = new List<Channel>();
        string? info = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                info = line;
                continue;
            }

            if (line.StartsWith("#"))
                continue;

            if (info is null || !Uri.TryCreate(line, UriKind.Absolute, out var uri))
                continue;

            var attributes = AttributeRegex.Matches(info)
                .ToDictionary(m => m.Groups["key"].Value, m => m.Groups["value"].Value, StringComparer.OrdinalIgnoreCase);

            var comma = info.LastIndexOf(',');
            var displayName = comma >= 0 ? info[(comma + 1)..].Trim() : "Canal";
            if (attributes.TryGetValue("tvg-name", out var tvgName) && !string.IsNullOrWhiteSpace(tvgName))
                displayName = tvgName.Trim();

            var id = Guid.NewGuid();
            var source = new ChannelSource(Guid.NewGuid(), provider, uri, 0);
            channels.Add(new Channel(
                id,
                displayName,
                TextNormalizer.Normalize(displayName),
                null,
                null,
                null,
                null,
                attributes.GetValueOrDefault("group-title"),
                attributes.GetValueOrDefault("tvg-logo"),
                attributes.GetValueOrDefault("tvg-id"),
                null,
                new[] { source }));

            info = null;
        }

        return channels;
    }
}

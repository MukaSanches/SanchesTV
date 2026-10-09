using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using SanchesTV.Core.Models;

namespace SanchesTV.Core.Parsing;

public static class XmlTvParser
{
    public static IReadOnlyList<EpgProgram> Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        // Each programme is materialized independently; no huge XDocument tree.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = 256_000_000,
            IgnoreComments = true
        };
        var result = new List<EpgProgram>();
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, settings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "programme")
                continue;

            var channel = reader.GetAttribute("channel");
            var startRaw = reader.GetAttribute("start");
            var stopRaw = reader.GetAttribute("stop");
            if (string.IsNullOrWhiteSpace(channel) ||
                string.IsNullOrWhiteSpace(startRaw) || string.IsNullOrWhiteSpace(stopRaw) ||
                !TryParseXmlTvDate(startRaw, out var start) ||
                !TryParseXmlTvDate(stopRaw, out var stop) || stop <= start)
                continue;

            using var subtree = reader.ReadSubtree();
            var node = XElement.Load(subtree, LoadOptions.None);
            var title = Localized(node, "title");
            if (string.IsNullOrWhiteSpace(title))
                continue;
            result.Add(new EpgProgram(channel.Trim(), start, stop, title,
                Localized(node, "desc"), Localized(node, "category")));
        }
        return result;
    }

    private static string? Localized(XElement parent, string name)
    {
        var candidates = parent.Elements().Where(x => x.Name.LocalName == name).ToArray();
        if (candidates.Length == 0) return null;
        var chosen = candidates.FirstOrDefault(x =>
                string.Equals((string?)x.Attribute("lang"), "pt-BR", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(x =>
                string.Equals((string?)x.Attribute("lang"), "pt", StringComparison.OrdinalIgnoreCase))
            ?? candidates[0];
        var text = chosen.Value.Trim();
        return text.Length == 0 ? null : text;
    }

    private static bool TryParseXmlTvDate(string input, out DateTimeOffset value)
    {
        input = input.Trim();
        var space = input.IndexOf(' ');
        if (space > 0)
        {
            var date = input[..space];
            var zone = input[(space + 1)..].Trim();
            if (zone == "Z") zone = "+00:00";
            else if (zone.Length == 5 && (zone[0] == '+' || zone[0] == '-'))
                zone = zone.Insert(3, ":");
            input = $"{date} {zone}";
        }
        string[] formats =
        {
            "yyyyMMddHHmmss zzz", "yyyyMMddHHmm zzz",
            "yyyyMMddHHmmss", "yyyyMMddHHmm"
        };
        return DateTimeOffset.TryParseExact(input, formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out value);
    }
}

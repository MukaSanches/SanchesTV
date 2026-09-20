using System.Globalization;
using System.Xml.Linq;
using SanchesTV.Core.Models;

namespace SanchesTV.Core.Parsing;

public static class XmlTvParser
{
    public static IReadOnlyList<EpgProgram> Parse(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        var result = new List<EpgProgram>();

        foreach (var node in doc.Descendants("programme"))
        {
            var channel = (string?)node.Attribute("channel");
            var startRaw = (string?)node.Attribute("start");
            var stopRaw = (string?)node.Attribute("stop");
            var title = node.Elements("title").FirstOrDefault()?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(channel) ||
                string.IsNullOrWhiteSpace(startRaw) ||
                string.IsNullOrWhiteSpace(stopRaw) ||
                string.IsNullOrWhiteSpace(title))
                continue;

            if (!TryParseXmlTvDate(startRaw, out var start) ||
                !TryParseXmlTvDate(stopRaw, out var stop))
                continue;

            result.Add(new EpgProgram(
                channel.Trim(),
                start,
                stop,
                title,
                node.Elements("desc").FirstOrDefault()?.Value?.Trim(),
                node.Elements("category").FirstOrDefault()?.Value?.Trim()));
        }

        return result;
    }

    private static bool TryParseXmlTvDate(string input, out DateTimeOffset value)
    {
        input = input.Trim();
        var space = input.IndexOf(' ');
        if (space > 0)
        {
            var datePart = input[..space];
            var tz = input[(space + 1)..].Trim();
            if (tz.Length == 5 && (tz[0] == '+' || tz[0] == '-'))
                tz = tz.Insert(3, ":");
            input = $"{datePart} {tz}";
        }

        string[] formats =
        {
            "yyyyMMddHHmmss zzz",
            "yyyyMMddHHmm zzz",
            "yyyyMMddHHmmss",
            "yyyyMMddHHmm"
        };

        return DateTimeOffset.TryParseExact(
            input,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
            out value);
    }
}

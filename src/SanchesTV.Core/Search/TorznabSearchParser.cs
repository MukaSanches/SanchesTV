using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace SanchesTV.Core.Search;

/// <summary>Parses standard RSS/Torznab responses without loading external DTDs.</summary>
public static class TorznabSearchParser
{
    public static IReadOnlyList<TorrentSearchResult> Parse(string xml, string provider)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return Array.Empty<TorrentSearchResult>();

        XDocument document;
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 2_000_000
            });
            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("O indexador retornou um RSS/Torznab inválido.", ex);
        }

        if (document.Descendants().Any(x => x.Name.LocalName == "error"))
            throw new InvalidDataException("O indexador retornou um erro de autenticação ou pesquisa.");

        var results = new List<TorrentSearchResult>();
        foreach (var item in document.Descendants().Where(x => x.Name.LocalName == "item").Take(60))
        {
            string Element(string name) => item.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Value?.Trim() ?? "";
            string Attribute(string name) => item.Elements()
                .Where(x => x.Name.LocalName == "attr")
                .Where(x => string.Equals((string?)x.Attribute("name"), name, StringComparison.OrdinalIgnoreCase))
                .Select(x => ((string?)x.Attribute("value"))?.Trim() ?? "")
                .FirstOrDefault() ?? "";

            var title = Element("title");
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var enclosure = item.Elements().FirstOrDefault(x => x.Name.LocalName == "enclosure");
            var candidates = new[]
            {
                Attribute("magneturl"),
                (string?)enclosure?.Attribute("url") ?? "",
                Element("link"),
                Element("guid")
            };
            var magnet = candidates.FirstOrDefault(x => x.StartsWith("magnet:?xt=urn:btih:", StringComparison.OrdinalIgnoreCase));
            var infoHash = Attribute("infohash");
            if (magnet is null && infoHash.Length == 40 && infoHash.All(Uri.IsHexDigit))
                magnet = "magnet:?xt=urn:btih:" + infoHash + "&dn=" + Uri.EscapeDataString(title);

            Uri? torrentFileUri = null;
            if (magnet is null)
            {
                foreach (var candidate in candidates)
                {
                    if (Uri.TryCreate(candidate, UriKind.Absolute, out var parsed) &&
                        parsed.Scheme is "https" or "http")
                    {
                        torrentFileUri = parsed;
                        break;
                    }
                }
            }

            if (magnet is null && torrentFileUri is null)
                continue;

            long? size = long.TryParse(Attribute("size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sizeAttr)
                ? sizeAttr
                : long.TryParse(Element("size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sizeElement)
                    ? sizeElement : null;
            int? seeders = int.TryParse(Attribute("seeders"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Max(0, value) : null;

            results.Add(new TorrentSearchResult(
                title, provider, magnet, torrentFileUri, size, seeders, null));
        }
        return results;
    }
}

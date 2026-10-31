using SanchesTV.Core.Search;
using Xunit;

namespace SanchesTV.Tests;

public sealed class TorznabSearchParserTests
{
    [Fact]
    public void ParsesInfoHashAndSeedersWithoutDownloadingAnything()
    {
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        const string xml = """
            <rss xmlns:torznab="http://torznab.com/schemas/2015/feed"><channel>
              <item><title>Filme autorizado</title>
                <torznab:attr name="infohash" value="0123456789abcdef0123456789abcdef01234567"/>
                <torznab:attr name="seeders" value="12"/>
                <torznab:attr name="size" value="1048576"/>
              </item>
            </channel></rss>
            """;
        var result = Assert.Single(TorznabSearchParser.Parse(xml, "Minha fonte"));
        Assert.Equal("Minha fonte", result.Provider);
        Assert.Equal(12, result.Seeders);
        Assert.Equal(1_048_576, result.SizeBytes);
        Assert.Contains(hash, result.Magnet);
        Assert.Null(result.TorrentFileUri);
    }

    [Fact]
    public void SupportsEnclosureWhenMagnetIsMissing()
    {
        const string xml = """
            <rss><channel><item><title>Arquivo permitido</title>
              <enclosure url="https://example.org/filme.torrent" type="application/x-bittorrent"/>
            </item></channel></rss>
            """;
        var result = Assert.Single(TorznabSearchParser.Parse(xml, "Fonte"));
        Assert.Equal("https://example.org/filme.torrent", result.TorrentFileUri?.AbsoluteUri);
    }

    [Fact]
    public void IgnoresItemsWithoutPlaybackSource()
    {
        Assert.Empty(TorznabSearchParser.Parse(
            "<rss><channel><item><title>Sem torrent</title></item></channel></rss>", "Fonte"));
    }

    [Fact]
    public void RejectsXmlDocumentTypes()
    {
        Assert.Throws<System.IO.InvalidDataException>(() => TorznabSearchParser.Parse(
            "<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///secrets'>]><rss/>", "Fonte"));
    }
}

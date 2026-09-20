using SanchesTV.Core.Parsing;

namespace SanchesTV.Tests;

public sealed class M3uParserTests
{
    [Fact]
    public void Parses_ExtInf_And_StreamUrl()
    {
        const string m3u = "#EXTM3U\n#EXTINF:-1 tvg-id=\"tvbrasil\" tvg-name=\"TV Brasil\" group-title=\"TV aberta\",TV Brasil\nhttps://example.org/live.m3u8";
        var channels = M3uParser.Parse(m3u);
        var channel = Assert.Single(channels);
        Assert.Equal("TV Brasil", channel.Name);
        Assert.Equal("tvbrasil", channel.EpgId);
        Assert.Equal("TV aberta", channel.Category);
        Assert.Equal("https://example.org/live.m3u8", channel.Sources[0].Url.ToString());
    }

    [Fact]
    public void Normalizer_Removes_Accents_And_Case()
    {
        Assert.Equal("tv cultura sao paulo", TextNormalizer.Normalize("TV Cultura São Paulo"));
    }
}

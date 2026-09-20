using SanchesTV.Core.Catalog;
using SanchesTV.Core.Models;
using SanchesTV.Core.Parsing;
using Xunit;

namespace SanchesTV.Tests;

public sealed class PortugueseCatalogTests
{
    [Fact]
    public void M3uParser_Preserves_Http_Headers_And_Canonicalizes_EpgId()
    {
        const string m3u = """
        #EXTM3U
        #EXTINF:-1 tvg-id="CanalTeste.br@HD" tvg-country="BR" tvg-language="Portuguese" group-title="Brasil",Canal Teste
        #EXTVLCOPT:http-user-agent=Mozilla/5.0
        #EXTVLCOPT:http-referrer=https://example.org/
        #EXTVLCOPT:http-origin=https://example.org
        https://cdn.example.org/live.m3u8
        """;

        var channel = Assert.Single(M3uParser.Parse(m3u, "Teste"));
        Assert.Equal("CanalTeste.br", channel.EpgId);
        Assert.Equal("BR", channel.Country);
        Assert.Equal("Portuguese", channel.Language);

        var source = Assert.Single(channel.Sources);
        Assert.Equal("Mozilla/5.0", source.UserAgent);
        Assert.Equal("https://example.org/", source.Referrer);
        Assert.Equal("https://example.org", source.Origin);
    }

    [Fact]
    public void M3uPt_Filter_Keeps_Lusophone_Tv_And_Rejects_NonPortuguese_Tv()
    {
        var pt = Make("RTP 1", "RTP1.pt", "TV");
        var unrelated = Make("France 24", "France24.fr", "TV");

        Assert.True(PortugueseCatalogRules.IsIncluded(pt, PortugueseCatalogFilter.M3uPtTelevision));
        Assert.False(PortugueseCatalogRules.IsIncluded(unrelated, PortugueseCatalogFilter.M3uPtTelevision));
    }

    [Fact]
    public void FreeTv_Filter_Keeps_Brazil_And_Portugal_Groups()
    {
        Assert.True(PortugueseCatalogRules.IsIncluded(Make("TV Cultura", "TVCultura.br", "Brazil"), PortugueseCatalogFilter.LusophoneCountryGroup));
        Assert.True(PortugueseCatalogRules.IsIncluded(Make("RTP 1", "RTP1.pt", "Portugal"), PortugueseCatalogFilter.LusophoneCountryGroup));
        Assert.False(PortugueseCatalogRules.IsIncluded(Make("BBC", "BBC.uk", "United Kingdom"), PortugueseCatalogFilter.LusophoneCountryGroup));
    }

    private static Channel Make(string name, string epgId, string category) =>
        new(
            Guid.NewGuid(),
            name,
            TextNormalizer.Normalize(name),
            null,
            null,
            null,
            null,
            category,
            null,
            epgId,
            null,
            new[] { new ChannelSource(Guid.NewGuid(), "Teste", new Uri("https://example.org/live.m3u8"), 0) });
}

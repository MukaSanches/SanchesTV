using SanchesTV.Core.Catalog;
using Xunit;

namespace SanchesTV.Tests;

public sealed class PremiumCatalogTests
{
    [Fact]
    public void PremiumCatalog_Has_OfficialProviders()
    {
        Assert.Contains(PremiumCatalog.Providers, x => x.Id == "globoplay");
        Assert.Contains(PremiumCatalog.Providers, x => x.Id == "claro-tv-plus");
        Assert.Contains(PremiumCatalog.Providers, x => x.Id == "sky-plus");
        Assert.Contains(PremiumCatalog.Providers, x => x.Id == "vivo-play");
        Assert.Contains(PremiumCatalog.Providers, x => x.Id == "zapping");
    }

    [Fact]
    public void PremiumCatalog_DoesNotEmbed_StreamUrls()
    {
        foreach (var provider in PremiumCatalog.Providers)
        {
            Assert.StartsWith("https://", provider.WebUrl);
            Assert.DoesNotContain(".m3u", provider.WebUrl, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".m3u8", provider.WebUrl, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PremiumChannelSearch_Finds_CommonPtBrChannels()
    {
        Assert.NotEmpty(PremiumCatalog.SearchChannels("ESPN"));
        Assert.NotEmpty(PremiumCatalog.SearchChannels("TNT"));
        Assert.NotEmpty(PremiumCatalog.SearchChannels("Discovery"));
        Assert.NotEmpty(PremiumCatalog.SearchChannels("sportv"));
    }
}

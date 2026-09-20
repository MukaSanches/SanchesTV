using SanchesTV.Core.Models;
using SanchesTV.Core.Parsing;

namespace SanchesTV.Core.Catalog;

public static class BuiltInCatalog
{
    public static IReadOnlyList<Channel> Create()
    {
        return new[]
        {
            Make("TV Câmara", "TVCamara.br", "Legislativo", "https://stream3.camara.gov.br/tv1/manifest.m3u8"),
            Make("TV Câmara 2", "TVCamara2.br", "Legislativo", "https://stream3.camara.gov.br/tv2/manifest.m3u8"),
            Make("TV Brasil Internacional", "TVBrasilInternacional.br", "Público", "https://tvbrasilinternacional-stream.ebc.com.br/index.m3u8"),
            Make("TV Senado", "TVSenado.br", "Legislativo", "rtsp://drix.senado.gov.br/tv1")
        };
    }

    private static Channel Make(string name, string epgId, string category, string url)
    {
        var channelId = Guid.NewGuid();
        return new Channel(
            channelId,
            name,
            TextNormalizer.Normalize(name),
            "BR",
            "pt-BR",
            null,
            null,
            category,
            null,
            epgId,
            null,
            new[]
            {
                new ChannelSource(Guid.NewGuid(), "Catálogo oficial", new Uri(url), 0)
            });
    }
}

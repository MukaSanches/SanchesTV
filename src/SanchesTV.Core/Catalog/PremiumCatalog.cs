namespace SanchesTV.Core.Catalog;

public sealed record PremiumProvider(
    string Id,
    string Name,
    string WebUrl,
    string? GuideUrl,
    string ChannelCountLabel,
    string Description,
    IReadOnlyList<string> Channels);

public static class PremiumCatalog
{
    public static IReadOnlyList<PremiumProvider> Providers { get; } =
    [
        new(
            "claro-tv-plus",
            "Claro tv+",
            "https://www.clarotvmais.com.br/",
            "https://www.claro.com.br/tv-por-assinatura/programacao",
            "120+ canais ao vivo anunciados",
            "TV aberta e por assinatura, com conteúdo adicional conforme o plano contratado.",
            [
                "ESPN", "ESPN 2", "ESPN 3", "ESPN 4", "ESPN 5",
                "sportv", "sportv 2", "sportv 3",
                "TNT", "TNT Séries", "TNT Novelas", "Warner Channel", "Space", "Cinemax",
                "Universal TV", "Studio Universal", "Sony", "AXN", "AMC", "A&E",
                "Comedy Central", "Paramount Network", "Megapix", "Canal Brasil",
                "GloboNews", "Multishow", "GNT", "Globoplay Novelas", "Modo Viagem",
                "Canal OFF", "E!", "TLC", "Arte1", "Food Network",
                "Discovery Home & Health", "ID", "HGTV", "Lifetime",
                "Cartoon Network", "Cartoonito", "Nickelodeon", "Nick Jr.",
                "Gloob", "Gloobinho", "TV Rá Tim Bum", "Tooncast",
                "Futura", "Curta!", "Fish TV", "WooHoo", "BIS", "MTV",
                "PlayTV", "Music Box Brazil", "Travel Box Brasil", "Sabor & Arte",
                "CNN Brasil Money", "Prime Box Brazil", "Telecine", "Premiere"
            ]),
        new(
            "zapping",
            "Zapping",
            "https://www.zapping.com.br/",
            "https://www.zapping.com.br/assinatura",
            "110+ canais no pacote Full anunciado",
            "TV ao vivo pela internet. O serviço também oferece adicionais como Telecine e Premiere conforme contratação.",
            [
                "TV Globo", "SBT", "Record", "RedeTV!", "TV Brasil",
                "GE TV", "Telecine Premium", "Telecine Action", "Telecine Touch",
                "Telecine Pipoca", "Telecine Cult", "Telecine Fun",
                "Premiere"
            ]),
        new(
            "sky-plus",
            "SKY+",
            "https://www.skymais.com.br/",
            "https://www.skymais.com.br/canais-disponiveis",
            "Planos Light e Full",
            "Serviço oficial de streaming da SKY com canais ao vivo e conteúdo sob demanda.",
            [
                "ESPN", "TNT", "Warner Channel", "Discovery Channel",
                "History", "Cartoon Network", "Nickelodeon",
                "CNN Brasil", "BandNews", "GloboNews", "sportv"
            ]),
        new(
            "vivo-play",
            "Vivo Play",
            "https://vivo.com.br/para-voce/produtos-e-servicos/para-casa/tv",
            "https://vivo.com.br/para-voce/produtos-e-servicos/para-casa/tv",
            "120+ canais nos pacotes de TV por assinatura; até 140 no Completo",
            "Vivo TV (antigo Vivo Play) com TV online e TV por assinatura, canais ao vivo e adicionais conforme o plano.",
            [
                "Discovery Channel", "History", "History 2", "National Geographic",
                "ESPN", "ESPN 2", "ESPN 3", "ESPN 4",
                "A&E", "AXN", "FX", "Prime Box Brasil", "Sony Channel",
                "Sony Movies", "Space", "Warner Channel", "TNT", "TNT Séries",
                "Cinemax", "Film&Arts", "Paramount Network", "TCM",
                "Cartoon Network", "Cartoonito", "Discovery Kids", "DreamWorks",
                "Nick Jr.", "Nickelodeon", "Tooncast", "TV Rá Tim Bum",
                "ZooMoo Kids", "CNN Brasil", "BandNews", "Jovem Pan News",
                "BM&C News", "Record News", "Discovery Home & Health",
                "E!", "FashionTV", "Fish TV", "Lifetime", "MTV", "MTV Live",
                "Food Network", "HGTV", "ID", "Adult Swim", "Woohoo",
                "TNT Novelas", "Trace Brasil", "Travel Box Brazil"
            ]),
        new(
            "globoplay",
            "Globoplay",
            "https://globoplay.globo.com/",
            "https://globoplay.globo.com/catalogo/",
            "17 canais pagos no plano Premium, além de TV Globo e Futura",
            "Acesso oficial aos canais Globo e produtos adicionais disponíveis conforme a assinatura.",
            [
                "TV Globo", "Futura", "GNT", "Multishow", "Globoplay Novelas",
                "BIS", "Canal OFF", "Gloob", "Gloobinho", "GloboNews",
                "Modo Viagem", "Canal Brasil", "Megapix", "Universal",
                "Studio Universal", "USA Network", "sportv", "sportv 2", "sportv 3",
                "Telecine", "Premiere", "Combate"
            ])
    ];

    public static IReadOnlyList<PremiumChannelEntry> SearchChannels(string? query = null)
    {
        var normalized = Normalize(query);

        var items = Providers
            .SelectMany(provider => provider.Channels.Select(channel =>
                new PremiumChannelEntry(channel, provider.Id, provider.Name, provider.WebUrl)))
            .GroupBy(x => $"{Normalize(x.Channel)}|{x.ProviderId}", StringComparer.Ordinal)
            .Select(g => g.First());

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            items = items.Where(x =>
                Normalize(x.Channel).Contains(normalized, StringComparison.Ordinal) ||
                Normalize(x.ProviderName).Contains(normalized, StringComparison.Ordinal));
        }

        return items
            .OrderBy(x => x.Channel, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.ProviderName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Concat(
                value.Normalize(System.Text.NormalizationForm.FormD)
                    .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                                System.Globalization.UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant()
            .Trim();
    }
}

public sealed record PremiumChannelEntry(
    string Channel,
    string ProviderId,
    string ProviderName,
    string ProviderUrl);

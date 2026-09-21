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
                "ESPN", "ESPN 2", "ESPN 3", "ESPN 4",
                "TNT", "TNT Séries", "Warner Channel", "Space", "Cinemax",
                "Discovery Channel", "Discovery Home & Health", "Discovery Kids",
                "Cartoon Network", "Cartoonito", "Nickelodeon", "Nick Jr.",
                "History", "A&E", "AXN", "Sony Channel", "Sony Movies",
                "Paramount Network", "MTV", "Food Network", "HGTV",
                "GloboNews", "Multishow", "GNT", "sportv", "Canal Brasil",
                "Telecine", "Premiere"
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
            "Pacotes Inicial e Estendido",
            "TV online da Vivo com canais por assinatura, abertos e adicionais conforme o plano.",
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
            "Canais Globo + adicionais conforme assinatura",
            "Acesso oficial aos canais e adicionais disponíveis na assinatura Globoplay.",
            [
                "TV Globo", "Multishow", "GloboNews", "sportv", "GNT",
                "Globoplay Novelas", "Gloob", "Canal Brasil", "Canal OFF",
                "Modo Viagem", "Premiere"
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

using System.Text.Json;
using SanchesTV.Core.Models;
using SanchesTV.Core.Parsing;

namespace SanchesTV.Core.Import;

public sealed class XtreamImporter(HttpClient httpClient)
{
    public async Task<IReadOnlyList<Channel>> ImportLiveAsync(
        string baseUrl,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        baseUrl = baseUrl.TrimEnd('/');
        var endpoint = $"{baseUrl}/player_api.php?username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}&action=get_live_streams";
        using var response = await httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var list = new List<Channel>();

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("stream_id", out var streamIdNode))
                continue;

            var streamId = streamIdNode.ToString();
            var name = item.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(name))
                name = $"Canal {streamId}";

            var logo = item.TryGetProperty("stream_icon", out var logoNode) ? logoNode.GetString() : null;
            var epg = item.TryGetProperty("epg_channel_id", out var epgNode) ? epgNode.GetString() : null;
            var categoryId = item.TryGetProperty("category_id", out var categoryNode) ? categoryNode.ToString() : null;

            var url = new Uri($"{baseUrl}/live/{Uri.EscapeDataString(username)}/{Uri.EscapeDataString(password)}/{streamId}.m3u8");
            list.Add(new Channel(
                Guid.NewGuid(),
                name!,
                TextNormalizer.Normalize(name!),
                null,
                null,
                null,
                null,
                string.IsNullOrWhiteSpace(categoryId) ? "Xtream" : $"Xtream {categoryId}",
                logo,
                epg,
                null,
                new[] { new ChannelSource(Guid.NewGuid(), "Xtream", url, 0) }));
        }

        return list;
    }
}

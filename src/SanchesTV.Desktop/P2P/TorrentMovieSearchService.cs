using System.IO;
using System.Net.Http;
using System.Text.Json;
using SanchesTV.Core.Search;

namespace SanchesTV.Desktop.P2P;

public sealed record TorrentSearchResponse(
    IReadOnlyList<TorrentSearchResult> Results,
    IReadOnlyList<string> Warnings);

public sealed class TorrentSearchConfiguration
{
    public List<TorznabSearchSource> Sources { get; set; } = new();
}

public sealed class TorznabSearchSource
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Optional metadata discovery. Does not change the BitTorrent engine or play media.
/// Internet Archive is the built-in catalog; users can configure their own Torznab providers.
/// </summary>
public sealed class TorrentMovieSearchService : IDisposable
{
    private const int MaxTorrentMetadataBytes = 2 * 1024 * 1024;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(25) };
    public string ConfigPath { get; }

    public TorrentMovieSearchService()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SanchesTV", "TorrentSearch");
        Directory.CreateDirectory(root);
        ConfigPath = Path.Combine(root, "sources.json");
        if (!File.Exists(ConfigPath))
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new TorrentSearchConfiguration(),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SanchesTV/8.4 (authorized-media-discovery)");
    }

    public async Task<TorrentSearchResponse> SearchAsync(string query, CancellationToken cancellationToken)
    {
        query = query?.Trim() ?? "";
        if (query.Length is < 2 or > 100)
            throw new ArgumentException("Pesquise um título com 2 a 100 caracteres.");

        var results = new List<TorrentSearchResult>();
        var warnings = new List<string>();

        try
        {
            results.AddRange(await SearchArchiveAsync(query, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            warnings.Add("Internet Archive: " + ex.Message);
        }

        TorrentSearchConfiguration configuration;
        try
        {
            configuration = JsonSerializer.Deserialize<TorrentSearchConfiguration>(
                await File.ReadAllTextAsync(ConfigPath, cancellationToken),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new TorrentSearchConfiguration();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            configuration = new TorrentSearchConfiguration();
            warnings.Add("As fontes adicionais não puderam ser lidas. Verifique sources.json.");
        }

        foreach (var source in (configuration.Sources ?? new()).Where(s => s.Enabled).Take(10))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                results.AddRange(await SearchTorznabAsync(source, query, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                warnings.Add((string.IsNullOrWhiteSpace(source.Name) ? "Fonte adicional" : source.Name) +
                    ": " + ex.Message);
            }
        }

        return new TorrentSearchResponse(
            results.Where(x => x.CanOpen)
                .DistinctBy(x => x.Magnet ?? x.TorrentFileUri?.AbsoluteUri)
                .OrderByDescending(x => x.Seeders ?? -1)
                .ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(100).ToArray(),
            warnings);
    }

    private async Task<IReadOnlyList<TorrentSearchResult>> SearchArchiveAsync(
        string query, CancellationToken cancellationToken)
    {
        // Quote the title for Archive.org's Lucene-style search API.
        var escapedTitle = query.Replace(@"\", @"\\").Replace("\"", "\\\"");
        var searchExpression = "mediatype:movies AND title:(\"" + escapedTitle + "\")";
        var uri = "https://archive.org/advancedsearch.php?output=json&rows=24" +
            "&fl%5B%5D=identifier&fl%5B%5D=title&fl%5B%5D=year" +
            "&q=" + Uri.EscapeDataString(searchExpression);

        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("response", out var responseObject) ||
            !responseObject.TryGetProperty("docs", out var docs) ||
            docs.ValueKind != JsonValueKind.Array)
            return Array.Empty<TorrentSearchResult>();

        var candidates = docs.EnumerateArray()
            .Where(x => x.TryGetProperty("identifier", out _))
            .Select(x => new
            {
                Id = x.GetProperty("identifier").GetString() ?? "",
                Title = ReadJsonString(x, "title"),
                Year = ReadJsonString(x, "year")
            })
            .Where(x => x.Id.Length > 0 && x.Id.Length <= 160)
            .Take(18).ToArray();

        using var limit = new SemaphoreSlim(4);
        var tasks = candidates.Select(async candidate =>
        {
            await limit.WaitAsync(cancellationToken);
            try
            {
                var uri = await FindArchiveTorrentAsync(candidate.Id, cancellationToken);
                return uri is null ? null : new TorrentSearchResult(
                    string.IsNullOrEmpty(candidate.Title) ? candidate.Id : candidate.Title,
                    "Internet Archive", null, uri, null, null, candidate.Year);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                // A single removed/inaccessible Archive item must not break the entire search.
                return null;
            }
            finally { limit.Release(); }
        });

        return (await Task.WhenAll(tasks)).OfType<TorrentSearchResult>().ToArray();
    }

    private async Task<Uri?> FindArchiveTorrentAsync(string identifier, CancellationToken cancellationToken)
    {
        var url = "https://archive.org/metadata/" + Uri.EscapeDataString(identifier);
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var file in files.EnumerateArray())
        {
            var name = ReadJsonString(file, "name");
            if (name is null || !name.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                continue;

            var escapedPath = string.Join("/", name.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
            return new Uri("https://archive.org/download/" +
                Uri.EscapeDataString(identifier) + "/" + escapedPath);
        }
        return null;
    }

    private async Task<IReadOnlyList<TorrentSearchResult>> SearchTorznabAsync(
        TorznabSearchSource source, string query, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("https" or "http") ||
            !string.IsNullOrEmpty(endpoint.UserInfo))
            throw new InvalidDataException("Endereço Torznab inválido. Informe a URL do endpoint /api.");

        var builder = new UriBuilder(endpoint);
        var baseQuery = builder.Query.TrimStart('?');
        var parameters = "t=search&cat=2000&limit=40&q=" + Uri.EscapeDataString(query);
        if (!string.IsNullOrWhiteSpace(source.ApiKey))
            parameters += "&apikey=" + Uri.EscapeDataString(source.ApiKey);
        builder.Query = string.IsNullOrEmpty(baseQuery) ? parameters : baseQuery + "&" + parameters;

        using var response = await _http.GetAsync(builder.Uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        return TorznabSearchParser.Parse(xml,
            string.IsNullOrWhiteSpace(source.Name) ? "Torznab" : source.Name.Trim());
    }

    /// <summary>Transfers only the small .torrent descriptor; never downloads the movie.</summary>
    public async Task<string> DownloadTorrentDescriptorAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("A fonte não forneceu uma URL HTTP(S) válida.");

        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxTorrentMetadataBytes)
            throw new InvalidDataException("O arquivo .torrent é maior que o limite de 2 MB.");

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[32 * 1024];
        while (true)
        {
            var length = await content.ReadAsync(chunk.AsMemory(), cancellationToken);
            if (length == 0) break;
            if (buffer.Length + length > MaxTorrentMetadataBytes)
                throw new InvalidDataException("Descritor .torrent excede 2 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, length), cancellationToken);
        }

        var destination = Path.Combine(Path.GetTempPath(),
            "SanchesTV-" + Guid.NewGuid().ToString("N") + ".torrent");
        await File.WriteAllBytesAsync(destination, buffer.ToArray(), cancellationToken);
        return destination;
    }

    private static string? ReadJsonString(JsonElement obj, string property)
    {
        if (!obj.TryGetProperty(property, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() :
            value.ValueKind == JsonValueKind.Number ? value.ToString() : null;
    }

    public void Dispose() => _http.Dispose();
}

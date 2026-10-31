namespace SanchesTV.Core.Search;

/// <summary>
/// A discovered BitTorrent source. The search layer never starts a transfer:
/// the existing P2P window remains responsible for consent, files and playback.
/// </summary>
public sealed record TorrentSearchResult(
    string Title,
    string Provider,
    string? Magnet,
    Uri? TorrentFileUri,
    long? SizeBytes,
    int? Seeders,
    string? Year)
{
    public bool CanOpen => !string.IsNullOrWhiteSpace(Magnet) || TorrentFileUri is not null;
    public string SizeText => SizeBytes is > 0
        ? P2P.P2pMediaPolicy.FormatBytes(SizeBytes.Value)
        : "—";
    public string SeedersText => Seeders.HasValue ? Seeders.Value.ToString("N0") : "—";
    public string YearText => Year ?? "—";
}

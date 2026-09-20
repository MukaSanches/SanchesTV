namespace SanchesTV.Core.Models;

public sealed record Channel(
    Guid Id,
    string Name,
    string NormalizedName,
    string? Country,
    string? Language,
    string? State,
    string? Region,
    string? Category,
    string? Logo,
    string? EpgId,
    int? VirtualNumber,
    IReadOnlyList<ChannelSource> Sources);

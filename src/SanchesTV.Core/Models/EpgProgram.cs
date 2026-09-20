namespace SanchesTV.Core.Models;

public sealed record EpgProgram(
    string ChannelEpgId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Title,
    string? Description,
    string? Category);

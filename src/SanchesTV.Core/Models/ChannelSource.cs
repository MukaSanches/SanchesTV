namespace SanchesTV.Core.Models;

public sealed record ChannelSource(
    Guid Id,
    string Provider,
    Uri Url,
    int Priority,
    StreamStatus Status = StreamStatus.NotTested,
    TimeSpan? Latency = null,
    string? Resolution = null,
    string? VideoCodec = null,
    string? AudioCodec = null,
    long? Bitrate = null,
    string? LastError = null);

public enum StreamStatus
{
    Online,
    Unstable,
    Slow,
    Offline,
    NotTested,
    Error,
    Blocked
}

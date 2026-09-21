namespace SanchesTV.Core.Models;

public enum ScheduledRecordingStatus
{
    Scheduled = 0,
    Recording = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public sealed record ScheduledRecording(
    Guid Id,
    Guid ChannelId,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? SeriesKey,
    ScheduledRecordingStatus Status,
    string? OutputPath,
    string? LastError);

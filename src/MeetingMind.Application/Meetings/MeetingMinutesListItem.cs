namespace MeetingMind.Application.Meetings;

public sealed record MeetingMinutesListItem(
    Guid JobId,
    string Title,
    string OriginalFileName,
    string SourceType,
    string ProcessingMode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset MinutesCreatedAt);

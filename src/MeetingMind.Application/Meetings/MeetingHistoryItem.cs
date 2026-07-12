namespace MeetingMind.Application.Meetings;

public sealed record MeetingHistoryItem(
    Guid JobId,
    string OriginalFileName,
    string Status,
    string Stage,
    int Progress,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

namespace MeetingMind.Application.Meetings;

public sealed record MeetingHistoryItem(
    Guid JobId,
    string OriginalFileName,
    string Status,
    string Stage,
    int Progress,
    string? ErrorCode,
    string? ErrorMessage,
    int AutomaticRetryCount,
    int AutomaticRetryLimit,
    DateTimeOffset? NextRetryAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    long ProcessingDurationSeconds,
    long TotalDurationSeconds);

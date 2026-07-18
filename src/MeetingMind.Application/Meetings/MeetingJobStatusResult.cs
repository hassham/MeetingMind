namespace MeetingMind.Application.Meetings;

public sealed record MeetingJobStatusResult(
    Guid JobId,
    string Status,
    string Stage,
    int Progress,
    string? ErrorMessage,
    int AutomaticRetryCount,
    int AutomaticRetryLimit,
    DateTimeOffset? NextRetryAt,
    long ProcessingDurationSeconds,
    long TotalDurationSeconds);

namespace MeetingMind.Application.Meetings;

public sealed record MeetingJobStatusResult(
    Guid JobId,
    string ProcessingMode,
    string Status,
    string Stage,
    int Progress,
    string? ErrorCode,
    string? ErrorMessage,
    int AutomaticRetryCount,
    int AutomaticRetryLimit,
    DateTimeOffset? NextRetryAt,
    long? SourceAudioDurationSeconds,
    long ProcessingDurationSeconds,
    long TotalDurationSeconds);

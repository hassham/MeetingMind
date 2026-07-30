namespace MeetingMind.Application.Dashboard;

public sealed record DashboardQuerySnapshot(
    int TotalJobs,
    int TranscriptOnlyJobs,
    int FullMeetingJobs,
    int MinutesFromTranscriptJobs,
    int CompletedJobs,
    int FailedJobs,
    int CancelledJobs,
    int QueuedJobs,
    int ProcessingJobs,
    long? TotalAudioDurationSeconds,
    double? AverageCompletedProcessingDurationSeconds,
    int TranscriptCount,
    int MinutesCount,
    IReadOnlyList<DashboardRecentJob> RecentJobs,
    IReadOnlyList<DashboardRecentMinutes> RecentMinutes);

public sealed record DashboardRecentJob(
    Guid JobId,
    string OriginalFileName,
    string ProcessingMode,
    string Status,
    string Stage,
    int Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DashboardRecentMinutes(
    Guid JobId,
    string Title,
    string OriginalFileName,
    string ProcessingMode,
    DateTimeOffset GeneratedAt);

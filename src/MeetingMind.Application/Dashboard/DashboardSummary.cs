namespace MeetingMind.Application.Dashboard;

public sealed record DashboardSummary(
    string TimeBasis,
    int TotalJobs,
    DashboardModeCounts JobsByMode,
    DashboardStatusCounts JobsByStatus,
    double? SuccessRatePercent,
    long? TotalAudioDurationSeconds,
    double? AverageCompletedProcessingDurationSeconds,
    int TranscriptCount,
    int MinutesCount,
    DashboardActionCounts Actions,
    IReadOnlyList<DashboardRecentJob> RecentJobs,
    IReadOnlyList<DashboardRecentMinutes> RecentMinutes);

public sealed record DashboardActionCounts(int Open, int InProgress, int Blocked, int Completed, int Cancelled, int Overdue);

public sealed record DashboardModeCounts(
    int TranscriptOnly,
    int FullMeeting,
    int MinutesFromTranscript);

public sealed record DashboardStatusCounts(
    int Completed,
    int Failed,
    int Cancelled,
    int Active,
    int Queued,
    int Processing);

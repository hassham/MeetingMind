using MeetingMind.Application.Common.Interfaces;

namespace MeetingMind.Application.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private const int RecentLimit = 5;
    private readonly IDashboardRepository _dashboardRepository;

    public DashboardService(IDashboardRepository dashboardRepository)
    {
        _dashboardRepository = dashboardRepository;
    }

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _dashboardRepository.GetSummaryAsync(RecentLimit, cancellationToken);
        var successDenominator = snapshot.CompletedJobs + snapshot.FailedJobs;
        double? successRate = successDenominator == 0
            ? null
            : snapshot.CompletedJobs * 100d / successDenominator;

        return new DashboardSummary(
            "All time",
            snapshot.TotalJobs,
            new DashboardModeCounts(
                snapshot.TranscriptOnlyJobs,
                snapshot.FullMeetingJobs,
                snapshot.MinutesFromTranscriptJobs),
            new DashboardStatusCounts(
                snapshot.CompletedJobs,
                snapshot.FailedJobs,
                snapshot.CancelledJobs,
                snapshot.QueuedJobs + snapshot.ProcessingJobs,
                snapshot.QueuedJobs,
                snapshot.ProcessingJobs),
            successRate,
            snapshot.TotalAudioDurationSeconds,
            snapshot.AverageCompletedProcessingDurationSeconds,
            snapshot.TranscriptCount,
            snapshot.MinutesCount,
            new DashboardActionCounts(snapshot.OpenActions, snapshot.InProgressActions, snapshot.BlockedActions, snapshot.CompletedActions, snapshot.CancelledActions, snapshot.OverdueActions),
            snapshot.RecentJobs,
            snapshot.RecentMinutes);
    }
}

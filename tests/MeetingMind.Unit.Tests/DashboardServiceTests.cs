using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Dashboard;

namespace MeetingMind.Unit.Tests;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task SummaryCalculatesApprovedStatusAndSuccessFormulas()
    {
        var repository = new StubDashboardRepository(new DashboardQuerySnapshot(
            10,
            2,
            5,
            3,
            4,
            1,
            2,
            1,
            2,
            300,
            45,
            7,
            4,
            [],
            []));

        var result = await new DashboardService(repository).GetSummaryAsync(CancellationToken.None);

        Assert.Equal("All time", result.TimeBasis);
        Assert.Equal(10, result.TotalJobs);
        Assert.Equal(80, result.SuccessRatePercent);
        Assert.Equal(3, result.JobsByStatus.Active);
        Assert.Equal(2, result.JobsByStatus.Cancelled);
        Assert.Equal(2, result.JobsByMode.TranscriptOnly);
        Assert.Equal(5, result.JobsByMode.FullMeeting);
        Assert.Equal(3, result.JobsByMode.MinutesFromTranscript);
    }

    [Fact]
    public async Task SummaryReturnsNullSuccessRateWithoutCompletedOrFailedJobs()
    {
        var repository = new StubDashboardRepository(new DashboardQuerySnapshot(
            2, 1, 1, 0, 0, 0, 0, 1, 1, null, null, 0, 0, [], []));

        var result = await new DashboardService(repository).GetSummaryAsync(CancellationToken.None);

        Assert.Null(result.SuccessRatePercent);
        Assert.Equal(2, result.JobsByStatus.Active);
    }

    private sealed class StubDashboardRepository : IDashboardRepository
    {
        private readonly DashboardQuerySnapshot _snapshot;

        public StubDashboardRepository(DashboardQuerySnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<DashboardQuerySnapshot> GetSummaryAsync(
            int recentLimit,
            CancellationToken cancellationToken)
        {
            Assert.Equal(5, recentLimit);
            return Task.FromResult(_snapshot);
        }
    }
}

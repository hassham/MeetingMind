using MeetingMind.Application.Dashboard;

namespace MeetingMind.Application.Common.Interfaces;

public interface IDashboardRepository
{
    Task<DashboardQuerySnapshot> GetSummaryAsync(
        int recentLimit,
        CancellationToken cancellationToken);
}

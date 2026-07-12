namespace MeetingMind.Application.Meetings;

public interface IMeetingHistoryService
{
    Task<MeetingHistoryResult> GetHistoryAsync(
        int skip,
        int take,
        CancellationToken cancellationToken);
}

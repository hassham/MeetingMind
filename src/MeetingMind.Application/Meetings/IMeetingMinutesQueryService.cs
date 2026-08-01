namespace MeetingMind.Application.Meetings;

public interface IMeetingMinutesQueryService
{
    Task<MeetingMinutesListResult> GetMinutesAsync(
        int skip,
        int take,
        CancellationToken cancellationToken);
}

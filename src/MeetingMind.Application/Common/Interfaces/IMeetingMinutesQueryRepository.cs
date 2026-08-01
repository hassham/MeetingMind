using MeetingMind.Application.Meetings;

namespace MeetingMind.Application.Common.Interfaces;

public interface IMeetingMinutesQueryRepository
{
    Task<IReadOnlyList<MeetingMinutesListItem>> GetPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);
}

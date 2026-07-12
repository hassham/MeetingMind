using MeetingMind.Application.Common.Interfaces;

namespace MeetingMind.Application.Meetings;

public class MeetingHistoryService : IMeetingHistoryService
{
    private const int DefaultTake = 50;
    private const int MaxTake = 100;

    private readonly IMeetingJobRepository _meetingJobRepository;

    public MeetingHistoryService(IMeetingJobRepository meetingJobRepository)
    {
        _meetingJobRepository = meetingJobRepository;
    }

    public async Task<MeetingHistoryResult> GetHistoryAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = take <= 0 ? DefaultTake : Math.Min(take, MaxTake);
        var items = await _meetingJobRepository.GetHistoryAsync(
            normalizedSkip,
            normalizedTake,
            cancellationToken);
        var total = await _meetingJobRepository.CountAsync(cancellationToken);

        return new MeetingHistoryResult(
            normalizedSkip,
            normalizedTake,
            total,
            items.Select(item => new MeetingHistoryItem(
                item.Id,
                item.OriginalFileName,
                item.Status.ToString(),
                item.Stage.ToString(),
                item.Progress,
                item.ErrorMessage,
                item.CreatedAt,
                item.UpdatedAt,
                item.StartedAt,
                item.CompletedAt)).ToArray());
    }
}

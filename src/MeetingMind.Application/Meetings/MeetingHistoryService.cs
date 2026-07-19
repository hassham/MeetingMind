using MeetingMind.Application.Common.Interfaces;

namespace MeetingMind.Application.Meetings;

public class MeetingHistoryService : IMeetingHistoryService
{
    private const int DefaultTake = 50;
    private const int MaxTake = 100;

    private readonly IMeetingJobRepository _meetingJobRepository;
    private readonly TimeProvider _timeProvider;

    public MeetingHistoryService(
        IMeetingJobRepository meetingJobRepository,
        TimeProvider timeProvider)
    {
        _meetingJobRepository = meetingJobRepository;
        _timeProvider = timeProvider;
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

        var now = _timeProvider.GetUtcNow();

        return new MeetingHistoryResult(
            normalizedSkip,
            normalizedTake,
            total,
            items.Select(item => CreateHistoryItem(item, now)).ToArray());
    }

    private static MeetingHistoryItem CreateHistoryItem(
        Domain.Entities.MeetingJob item,
        DateTimeOffset now)
    {
        var duration = MeetingDurationCalculator.Calculate(item, now);

        return new MeetingHistoryItem(
            item.Id,
            item.OriginalFileName,
            item.Status.ToString(),
            item.Stage.ToString(),
            item.Progress,
            item.ErrorCode,
            item.ErrorMessage,
            item.AutomaticRetryCount,
            item.AutomaticRetryLimit,
            item.NextRetryAt,
            item.CreatedAt,
            item.UpdatedAt,
            item.StartedAt,
            item.CompletedAt,
            duration.ProcessingDurationSeconds,
            duration.TotalDurationSeconds);
    }
}

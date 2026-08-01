using MeetingMind.Application.Common.Interfaces;

namespace MeetingMind.Application.Meetings;

public sealed class MeetingMinutesQueryService : IMeetingMinutesQueryService
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;
    private readonly IMeetingMinutesQueryRepository _repository;

    public MeetingMinutesQueryService(IMeetingMinutesQueryRepository repository)
    {
        _repository = repository;
    }

    public async Task<MeetingMinutesListResult> GetMinutesAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = take <= 0 ? DefaultTake : Math.Min(take, MaxTake);
        var items = await _repository.GetPageAsync(
            normalizedSkip,
            normalizedTake,
            cancellationToken);
        var total = await _repository.CountAsync(cancellationToken);

        return new MeetingMinutesListResult(
            normalizedSkip,
            normalizedTake,
            total,
            items);
    }
}

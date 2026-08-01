using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Meetings;

namespace MeetingMind.Unit.Tests;

public sealed class MeetingMinutesQueryServiceTests
{
    [Theory]
    [InlineData(-5, 0, 0, 20)]
    [InlineData(10, 500, 10, 100)]
    [InlineData(5, 25, 5, 25)]
    public async Task NormalizesPaging(int skip, int take, int expectedSkip, int expectedTake)
    {
        var repository = new StubRepository();
        var result = await new MeetingMinutesQueryService(repository)
            .GetMinutesAsync(skip, take, CancellationToken.None);

        Assert.Equal(expectedSkip, result.Skip);
        Assert.Equal(expectedTake, result.Take);
        Assert.Equal((expectedSkip, expectedTake), repository.LastPage);
        Assert.Equal(7, result.Total);
    }

    private sealed class StubRepository : IMeetingMinutesQueryRepository
    {
        public (int Skip, int Take) LastPage { get; private set; }
        public Task<IReadOnlyList<MeetingMinutesListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken)
        {
            LastPage = (skip, take);
            return Task.FromResult<IReadOnlyList<MeetingMinutesListItem>>([]);
        }
        public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(7);
    }
}

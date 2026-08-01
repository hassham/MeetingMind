using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeetingMind.Infrastructure.Persistence;

public sealed class EfMeetingMinutesQueryRepository : IMeetingMinutesQueryRepository
{
    private readonly MeetingMindDbContext _dbContext;

    public EfMeetingMinutesQueryRepository(MeetingMindDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MeetingMinutesListItem>> GetPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await _dbContext.MeetingMinutes
            .AsNoTracking()
            .OrderByDescending(minutes => minutes.CreatedAt)
            .ThenByDescending(minutes => minutes.Id)
            .Skip(skip)
            .Take(take)
            .Select(minutes => new MeetingMinutesListItem(
                minutes.MeetingJobId,
                minutes.Title,
                minutes.MeetingJob!.OriginalFileName,
                minutes.MeetingJob.ProcessingMode == MeetingProcessingMode.MinutesFromTranscript
                    ? "Transcript"
                    : "Audio",
                minutes.MeetingJob.ProcessingMode.ToString(),
                minutes.MeetingJob.CreatedAt,
                minutes.MeetingJob.StartedAt,
                minutes.MeetingJob.CompletedAt,
                minutes.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.MeetingMinutes.CountAsync(cancellationToken);
    }
}

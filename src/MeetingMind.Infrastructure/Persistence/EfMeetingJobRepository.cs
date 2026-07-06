using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Entities;

namespace MeetingMind.Infrastructure.Persistence;

public class EfMeetingJobRepository : IMeetingJobRepository
{
    private readonly MeetingMindDbContext _dbContext;

    public EfMeetingJobRepository(MeetingMindDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(MeetingJob meetingJob, CancellationToken cancellationToken)
    {
        await _dbContext.MeetingJobs.AddAsync(meetingJob, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

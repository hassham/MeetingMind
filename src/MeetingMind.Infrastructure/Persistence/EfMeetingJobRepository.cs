using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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

    public Task<MeetingJob?> GetByIdAsync(Guid meetingJobId, CancellationToken cancellationToken)
    {
        return _dbContext.MeetingJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(meetingJob => meetingJob.Id == meetingJobId, cancellationToken);
    }

    public async Task SetHangfireJobIdAsync(
        Guid meetingJobId,
        string hangfireJobId,
        CancellationToken cancellationToken)
    {
        var meetingJob = await GetMeetingJobAsync(meetingJobId, cancellationToken);
        meetingJob.HangfireJobId = hangfireJobId;
        meetingJob.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        Guid meetingJobId,
        MeetingJobStatus status,
        MeetingJobStage stage,
        int progress,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var meetingJob = await GetMeetingJobAsync(meetingJobId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        meetingJob.Status = status;
        meetingJob.Stage = stage;
        meetingJob.Progress = progress;
        meetingJob.ErrorMessage = errorMessage;
        meetingJob.UpdatedAt = now;

        if (status == MeetingJobStatus.Processing && meetingJob.StartedAt is null)
        {
            meetingJob.StartedAt = now;
        }

        if (status is MeetingJobStatus.Completed or MeetingJobStatus.Failed or MeetingJobStatus.Cancelled)
        {
            meetingJob.CompletedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<MeetingJob> GetMeetingJobAsync(Guid meetingJobId, CancellationToken cancellationToken)
    {
        return await _dbContext.MeetingJobs.SingleOrDefaultAsync(
                meetingJob => meetingJob.Id == meetingJobId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Meeting job '{meetingJobId}' was not found.");
    }
}

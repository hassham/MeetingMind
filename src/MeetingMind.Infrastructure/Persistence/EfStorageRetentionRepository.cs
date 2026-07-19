using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Retention;
using MeetingMind.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeetingMind.Infrastructure.Persistence;

public sealed class EfStorageRetentionRepository : IStorageRetentionRepository
{
    private readonly MeetingMindDbContext _dbContext;

    public EfStorageRetentionRepository(MeetingMindDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Guid>> GetExpiredTerminalJobIdsAsync(
        DateTimeOffset cutoff,
        int take,
        CancellationToken cancellationToken)
    {
        return await EligibleJobs(cutoff)
            .AsNoTracking()
            .OrderBy(job => job.CompletedAt ?? job.UpdatedAt)
            .Select(job => job.Id)
            .Take(take)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> DeleteEligibleJobWithArtifactsAsync(
        Guid jobId,
        DateTimeOffset cutoff,
        Func<StorageRetentionCandidate, CancellationToken, Task> deleteArtifacts,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var job = await _dbContext.MeetingJobs
            .FromSqlInterpolated($"SELECT * FROM \"MeetingJobs\" WHERE \"Id\" = {jobId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (job is null || !IsEligible(job, cutoff))
        {
            return false;
        }

        var transcriptPath = await _dbContext.MeetingTranscripts
            .Where(transcript => transcript.MeetingJobId == jobId)
            .Select(transcript => transcript.TranscriptFilePath)
            .SingleOrDefaultAsync(cancellationToken);
        var minutesPath = await _dbContext.MeetingMinutes
            .Where(minutes => minutes.MeetingJobId == jobId)
            .Select(minutes => minutes.MinutesFilePath)
            .SingleOrDefaultAsync(cancellationToken);
        var candidate = new StorageRetentionCandidate(
            job.Id,
            job.Status,
            job.CompletedAt ?? job.UpdatedAt,
            job.OriginalFilePath,
            job.ProcessedFilePath,
            transcriptPath,
            minutesPath);

        await deleteArtifacts(candidate, cancellationToken);

        _dbContext.MeetingJobs.Remove(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static bool IsEligible(Domain.Entities.MeetingJob job, DateTimeOffset cutoff)
    {
        return job.Status is MeetingJobStatus.Completed or MeetingJobStatus.Failed or MeetingJobStatus.Cancelled &&
               job.NextRetryAt is null &&
               (job.CompletedAt ?? job.UpdatedAt) <= cutoff;
    }

    private IQueryable<Domain.Entities.MeetingJob> EligibleJobs(DateTimeOffset cutoff)
    {
        return _dbContext.MeetingJobs.Where(job =>
            (job.Status == MeetingJobStatus.Completed ||
             job.Status == MeetingJobStatus.Failed ||
             job.Status == MeetingJobStatus.Cancelled) &&
            job.NextRetryAt == null &&
            (job.CompletedAt ?? job.UpdatedAt) <= cutoff);
    }
}

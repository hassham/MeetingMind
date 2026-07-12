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

    public Task<MeetingTranscript?> GetTranscriptByJobIdAsync(
        Guid meetingJobId,
        CancellationToken cancellationToken)
    {
        return _dbContext.MeetingTranscripts
            .AsNoTracking()
            .SingleOrDefaultAsync(transcript => transcript.MeetingJobId == meetingJobId, cancellationToken);
    }

    public Task<MeetingMinutes?> GetMinutesByJobIdAsync(
        Guid meetingJobId,
        CancellationToken cancellationToken)
    {
        return _dbContext.MeetingMinutes
            .AsNoTracking()
            .SingleOrDefaultAsync(minutes => minutes.MeetingJobId == meetingJobId, cancellationToken);
    }

    public async Task<IReadOnlyList<MeetingJob>> GetHistoryAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await _dbContext.MeetingJobs
            .AsNoTracking()
            .OrderByDescending(meetingJob => meetingJob.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.MeetingJobs.CountAsync(cancellationToken);
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

    public async Task SetProcessedFilePathAsync(
        Guid meetingJobId,
        string processedFilePath,
        CancellationToken cancellationToken)
    {
        var meetingJob = await GetMeetingJobAsync(meetingJobId, cancellationToken);
        meetingJob.ProcessedFilePath = processedFilePath;
        meetingJob.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveTranscriptAsync(
        Guid meetingJobId,
        string transcriptText,
        string transcriptFilePath,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var transcript = await _dbContext.MeetingTranscripts.SingleOrDefaultAsync(
            existingTranscript => existingTranscript.MeetingJobId == meetingJobId,
            cancellationToken);

        if (transcript is null)
        {
            transcript = new MeetingTranscript
            {
                Id = Guid.NewGuid(),
                MeetingJobId = meetingJobId,
                CreatedAt = now
            };

            await _dbContext.MeetingTranscripts.AddAsync(transcript, cancellationToken);
        }

        transcript.TranscriptText = transcriptText;
        transcript.TranscriptFilePath = transcriptFilePath;

        var meetingJob = await GetMeetingJobAsync(meetingJobId, cancellationToken);
        meetingJob.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveMinutesAsync(
        Guid meetingJobId,
        MeetingMinutes minutes,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existingMinutes = await _dbContext.MeetingMinutes.SingleOrDefaultAsync(
            currentMinutes => currentMinutes.MeetingJobId == meetingJobId,
            cancellationToken);

        if (existingMinutes is null)
        {
            existingMinutes = new MeetingMinutes
            {
                Id = Guid.NewGuid(),
                MeetingJobId = meetingJobId,
                CreatedAt = now
            };

            await _dbContext.MeetingMinutes.AddAsync(existingMinutes, cancellationToken);
        }

        existingMinutes.Title = minutes.Title;
        existingMinutes.Summary = minutes.Summary;
        existingMinutes.DecisionsJson = minutes.DecisionsJson;
        existingMinutes.ActionItemsJson = minutes.ActionItemsJson;
        existingMinutes.RisksJson = minutes.RisksJson;
        existingMinutes.NextStepsJson = minutes.NextStepsJson;
        existingMinutes.FullMinutesJson = minutes.FullMinutesJson;
        existingMinutes.MinutesFilePath = minutes.MinutesFilePath;

        var meetingJob = await GetMeetingJobAsync(meetingJobId, cancellationToken);
        meetingJob.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetForRetryAsync(Guid meetingJobId, CancellationToken cancellationToken)
    {
        var meetingJob = await GetMeetingJobAsync(meetingJobId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        meetingJob.Status = MeetingJobStatus.Queued;
        meetingJob.Stage = MeetingJobStage.Uploaded;
        meetingJob.Progress = 0;
        meetingJob.ErrorMessage = null;
        meetingJob.HangfireJobId = null;
        meetingJob.StartedAt = null;
        meetingJob.CompletedAt = null;
        meetingJob.UpdatedAt = now;

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

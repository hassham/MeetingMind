using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Dashboard;
using MeetingMind.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeetingMind.Infrastructure.Persistence;

public sealed class EfDashboardRepository : IDashboardRepository
{
    private readonly MeetingMindDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public EfDashboardRepository(MeetingMindDbContext dbContext, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DashboardQuerySnapshot> GetSummaryAsync(
        int recentLimit,
        CancellationToken cancellationToken)
    {
        var boundedRecentLimit = Math.Clamp(recentLimit, 0, 5);
        var jobs = _dbContext.MeetingJobs.AsNoTracking();

        var statusCounts = await jobs
            .GroupBy(job => job.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        var modeCounts = await jobs
            .GroupBy(job => job.ProcessingMode)
            .Select(group => new { Mode = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Mode, item => item.Count, cancellationToken);

        var totalJobs = await jobs.CountAsync(cancellationToken);
        var knownAudioDurations = jobs.Where(job =>
            job.ProcessingMode != MeetingProcessingMode.MinutesFromTranscript &&
            job.SourceAudioDurationSeconds != null);
        var knownAudioCount = await knownAudioDurations.CountAsync(cancellationToken);
        var totalAudioDuration = knownAudioCount == 0
            ? null
            : await knownAudioDurations.SumAsync(
                job => job.SourceAudioDurationSeconds,
                cancellationToken);

        var validCompletedDurations = jobs.Where(job =>
            job.Status == MeetingJobStatus.Completed &&
            job.StartedAt != null &&
            job.CompletedAt != null &&
            job.CompletedAt >= job.StartedAt);
        var validDurationCount = await validCompletedDurations.CountAsync(cancellationToken);
        double? averageCompletedDuration = validDurationCount == 0
            ? null
            : await validCompletedDurations.AverageAsync(
                job => (job.CompletedAt!.Value - job.StartedAt!.Value).TotalSeconds,
                cancellationToken);

        var transcriptCount = await _dbContext.MeetingTranscripts.CountAsync(cancellationToken);
        var minutesCount = await _dbContext.MeetingMinutes.CountAsync(cancellationToken);
        var actionCounts = await _dbContext.ActionItems.AsNoTracking().GroupBy(action => action.Status).Select(group => new { Status = group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        var utcToday = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var overdueActions = await _dbContext.ActionItems.CountAsync(action => action.DueDate < utcToday && action.Status != ActionItemStatus.Completed && action.Status != ActionItemStatus.Cancelled, cancellationToken);

        var recentJobs = await jobs
            .OrderByDescending(job => job.CreatedAt)
            .ThenByDescending(job => job.Id)
            .Take(boundedRecentLimit)
            .Select(job => new DashboardRecentJob(
                job.Id,
                job.OriginalFileName,
                job.ProcessingMode.ToString(),
                job.Status.ToString(),
                job.Stage.ToString(),
                job.Progress,
                job.CreatedAt,
                job.UpdatedAt))
            .ToArrayAsync(cancellationToken);

        var recentMinutes = await _dbContext.MeetingMinutes
            .AsNoTracking()
            .OrderByDescending(minutes => minutes.CreatedAt)
            .ThenByDescending(minutes => minutes.Id)
            .Take(boundedRecentLimit)
            .Select(minutes => new DashboardRecentMinutes(
                minutes.MeetingJobId,
                minutes.Title,
                minutes.MeetingJob!.OriginalFileName,
                minutes.MeetingJob.ProcessingMode.ToString(),
                minutes.CreatedAt))
            .ToArrayAsync(cancellationToken);

        return new DashboardQuerySnapshot(
            totalJobs,
            GetCount(modeCounts, MeetingProcessingMode.TranscriptOnly),
            GetCount(modeCounts, MeetingProcessingMode.FullMeeting),
            GetCount(modeCounts, MeetingProcessingMode.MinutesFromTranscript),
            GetCount(statusCounts, MeetingJobStatus.Completed),
            GetCount(statusCounts, MeetingJobStatus.Failed),
            GetCount(statusCounts, MeetingJobStatus.Cancelled),
            GetCount(statusCounts, MeetingJobStatus.Queued),
            GetCount(statusCounts, MeetingJobStatus.Processing),
            totalAudioDuration,
            averageCompletedDuration,
            transcriptCount,
            minutesCount,
            GetCount(actionCounts, ActionItemStatus.Open),
            GetCount(actionCounts, ActionItemStatus.InProgress),
            GetCount(actionCounts, ActionItemStatus.Blocked),
            GetCount(actionCounts, ActionItemStatus.Completed),
            GetCount(actionCounts, ActionItemStatus.Cancelled),
            overdueActions,
            recentJobs,
            recentMinutes);
    }

    private static int GetCount<T>(IReadOnlyDictionary<T, int> counts, T key)
        where T : notnull =>
        counts.TryGetValue(key, out var count) ? count : 0;
}

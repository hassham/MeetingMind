using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using MeetingMind.Infrastructure.Persistence;

namespace MeetingMind.Infrastructure.IntegrationTests;

public sealed class EfMeetingJobRepositoryTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public EfMeetingJobRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAndStatusUpdatesPersistLifecycleTimestamps()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfMeetingJobRepository(dbContext);
        var job = CreateJob("status.mp3");

        await repository.AddAsync(job, CancellationToken.None);
        await repository.UpdateStatusAsync(
            job.Id,
            MeetingJobStatus.Processing,
            MeetingJobStage.Transcribing,
            25,
            null,
            CancellationToken.None);
        await repository.UpdateStatusAsync(
            job.Id,
            MeetingJobStatus.Completed,
            MeetingJobStage.Completed,
            100,
            null,
            CancellationToken.None);

        var saved = await repository.GetByIdAsync(job.Id, CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(MeetingJobStatus.Completed, saved.Status);
        Assert.Equal(MeetingJobStage.Completed, saved.Stage);
        Assert.Equal(100, saved.Progress);
        Assert.NotNull(saved.StartedAt);
        Assert.NotNull(saved.CompletedAt);
        Assert.True(saved.CompletedAt >= saved.StartedAt);
    }

    [Fact]
    public async Task TranscriptAndMinutesAreUpsertedPerJob()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfMeetingJobRepository(dbContext);
        var job = CreateJob("results.wav");
        await repository.AddAsync(job, CancellationToken.None);

        await repository.SaveTranscriptAsync(
            job.Id,
            "first transcript",
            "Transcript/first.txt",
            CancellationToken.None);
        await repository.SaveTranscriptAsync(
            job.Id,
            "updated transcript",
            "Transcript/updated.txt",
            CancellationToken.None);

        await repository.SaveMinutesAsync(job.Id, CreateMinutes(job.Id, "First title"), CancellationToken.None);
        await repository.SaveMinutesAsync(job.Id, CreateMinutes(job.Id, "Updated title"), CancellationToken.None);

        var transcript = await repository.GetTranscriptByJobIdAsync(job.Id, CancellationToken.None);
        var minutes = await repository.GetMinutesByJobIdAsync(job.Id, CancellationToken.None);

        Assert.NotNull(transcript);
        Assert.Equal("updated transcript", transcript.TranscriptText);
        Assert.Equal("Transcript/updated.txt", transcript.TranscriptFilePath);
        Assert.NotNull(minutes);
        Assert.Equal("Updated title", minutes.Title);
        Assert.Equal("Minutes/minutes.md", minutes.MinutesFilePath);
        Assert.Equal(1, dbContext.MeetingTranscripts.Count());
        Assert.Equal(1, dbContext.MeetingMinutes.Count());
    }

    [Fact]
    public async Task HistoryIsNewestFirstAndCounted()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfMeetingJobRepository(dbContext);
        var older = CreateJob("older.mp3", DateTimeOffset.UtcNow.AddMinutes(-2));
        var newer = CreateJob("newer.mp3", DateTimeOffset.UtcNow);
        await repository.AddAsync(older, CancellationToken.None);
        await repository.AddAsync(newer, CancellationToken.None);

        var history = await repository.GetHistoryAsync(0, 1, CancellationToken.None);
        var count = await repository.CountAsync(CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Single(history);
        Assert.Equal(newer.Id, history[0].Id);
    }

    [Fact]
    public async Task ResetForRetryPreservesPreviousTimingUntilNewAttemptStarts()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfMeetingJobRepository(dbContext);
        var job = CreateJob("retry.m4a");
        job.Status = MeetingJobStatus.Failed;
        job.Stage = MeetingJobStage.GeneratingMinutes;
        job.Progress = 60;
        job.ErrorMessage = "temporary failure";
        job.HangfireJobId = "old-hangfire-id";
        job.AutomaticRetryCount = 2;
        job.AutomaticRetryLimit = 2;
        job.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var previousStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var previousCompletedAt = DateTimeOffset.UtcNow;
        job.StartedAt = previousStartedAt;
        job.CompletedAt = previousCompletedAt;
        await repository.AddAsync(job, CancellationToken.None);

        await repository.ResetForRetryAsync(job.Id, CancellationToken.None);
        var reset = await repository.GetByIdAsync(job.Id, CancellationToken.None);

        Assert.NotNull(reset);
        Assert.Equal(job.Id, reset.Id);
        Assert.Equal(MeetingJobStatus.Queued, reset.Status);
        Assert.Equal(MeetingJobStage.Uploaded, reset.Stage);
        Assert.Equal(0, reset.Progress);
        Assert.Null(reset.ErrorMessage);
        Assert.Null(reset.ErrorCode);
        Assert.Null(reset.HangfireJobId);
        Assert.Equal(0, reset.AutomaticRetryCount);
        Assert.Equal(0, reset.AutomaticRetryLimit);
        Assert.Null(reset.NextRetryAt);
        Assert.Equal(previousStartedAt.ToUnixTimeSeconds(), reset.StartedAt?.ToUnixTimeSeconds());
        Assert.Equal(previousCompletedAt.ToUnixTimeSeconds(), reset.CompletedAt?.ToUnixTimeSeconds());

        await repository.BeginProcessingAsync(
            job.Id,
            2,
            CancellationToken.None);
        var restarted = await repository.GetByIdAsync(job.Id, CancellationToken.None);

        Assert.NotNull(restarted);
        Assert.True(restarted.StartedAt > previousCompletedAt);
        Assert.Null(restarted.CompletedAt);
        Assert.Equal(2, restarted.AutomaticRetryLimit);
    }

    [Fact]
    public async Task AutomaticRetryPreservesTimingAndPersistsScheduledAndFinalStates()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfMeetingJobRepository(dbContext);
        var job = CreateJob("automatic-retry.mp3");
        await repository.AddAsync(job, CancellationToken.None);

        await repository.BeginProcessingAsync(job.Id, 2, CancellationToken.None);
        var started = await repository.GetByIdAsync(job.Id, CancellationToken.None);
        Assert.NotNull(started?.StartedAt);

        var nextRetryAt = DateTimeOffset.UtcNow.AddSeconds(10);
        await repository.ScheduleAutomaticRetryAsync(
            job.Id,
            MeetingJobStage.Transcribing,
            25,
            "temporary_interruption",
            "Temporary transcription failure.",
            1,
            2,
            nextRetryAt,
            CancellationToken.None);

        var scheduled = await repository.GetByIdAsync(job.Id, CancellationToken.None);
        Assert.NotNull(scheduled);
        Assert.Equal(MeetingJobStatus.Queued, scheduled.Status);
        Assert.Equal(MeetingJobStage.Transcribing, scheduled.Stage);
        Assert.Equal("temporary_interruption", scheduled.ErrorCode);
        Assert.Equal(1, scheduled.AutomaticRetryCount);
        Assert.Equal(2, scheduled.AutomaticRetryLimit);
        Assert.Equal(nextRetryAt.ToUnixTimeSeconds(), scheduled.NextRetryAt?.ToUnixTimeSeconds());
        Assert.Equal(started!.StartedAt?.ToUnixTimeSeconds(), scheduled.StartedAt?.ToUnixTimeSeconds());
        Assert.Null(scheduled.CompletedAt);

        await repository.BeginProcessingAsync(job.Id, 2, CancellationToken.None);
        var resumed = await repository.GetByIdAsync(job.Id, CancellationToken.None);
        Assert.NotNull(resumed);
        Assert.Equal(MeetingJobStatus.Processing, resumed.Status);
        Assert.Equal(started.StartedAt?.ToUnixTimeSeconds(), resumed.StartedAt?.ToUnixTimeSeconds());
        Assert.Null(resumed.NextRetryAt);

        await repository.RecordFinalFailureAsync(
            job.Id,
            MeetingJobStage.Transcribing,
            25,
            "retry_exhausted",
            "Retries exhausted.",
            2,
            2,
            CancellationToken.None);

        var failed = await repository.GetByIdAsync(job.Id, CancellationToken.None);
        Assert.NotNull(failed);
        Assert.Equal(MeetingJobStatus.Failed, failed.Status);
        Assert.Equal("retry_exhausted", failed.ErrorCode);
        Assert.Equal(2, failed.AutomaticRetryCount);
        Assert.Null(failed.NextRetryAt);
        Assert.NotNull(failed.CompletedAt);
    }

    [Fact]
    public async Task UpdatingMissingJobFailsWithoutCreatingData()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfMeetingJobRepository(dbContext);
        var missingJobId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.UpdateStatusAsync(
                missingJobId,
                MeetingJobStatus.Processing,
                MeetingJobStage.Validating,
                0,
                null,
                CancellationToken.None));

        Assert.Contains(missingJobId.ToString(), exception.Message);
        Assert.Equal(0, await repository.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RetentionRepositorySelectsOnlyExpiredTerminalJobsAndRevalidatesDeletion()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfStorageRetentionRepository(dbContext);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var expired = CreateJob("expired.mp3", cutoff.AddDays(-1));
        expired.Status = MeetingJobStatus.Completed;
        expired.CompletedAt = cutoff.AddDays(-1);
        var active = CreateJob("active.mp3", cutoff.AddDays(-2));
        active.Status = MeetingJobStatus.Processing;
        active.UpdatedAt = cutoff.AddDays(-2);
        var scheduledRetry = CreateJob("retry.mp3", cutoff.AddDays(-2));
        scheduledRetry.Status = MeetingJobStatus.Failed;
        scheduledRetry.CompletedAt = cutoff.AddDays(-2);
        scheduledRetry.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(1);
        dbContext.AddRange(expired, active, scheduledRetry);
        await dbContext.SaveChangesAsync();

        var ids = await repository.GetExpiredTerminalJobIdsAsync(cutoff, 100, CancellationToken.None);
        var activeDeleted = await repository.DeleteEligibleJobWithArtifactsAsync(
            active.Id,
            cutoff,
            (_, _) => Task.CompletedTask,
            CancellationToken.None);
        var deleted = await repository.DeleteEligibleJobWithArtifactsAsync(
            expired.Id,
            cutoff,
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.Equal([expired.Id], ids);
        Assert.False(activeDeleted);
        Assert.True(deleted);
        Assert.NotNull(await dbContext.MeetingJobs.FindAsync(active.Id));
        Assert.NotNull(await dbContext.MeetingJobs.FindAsync(scheduledRetry.Id));
        Assert.Null(await dbContext.MeetingJobs.FindAsync(expired.Id));
    }

    private static MeetingJob CreateJob(string fileName, DateTimeOffset? createdAt = null)
    {
        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        return new MeetingJob
        {
            Id = Guid.NewGuid(),
            OriginalFileName = fileName,
            OriginalFilePath = $"Audio/Original/{Guid.NewGuid():N}{Path.GetExtension(fileName)}",
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    private static MeetingMinutes CreateMinutes(Guid jobId, string title)
    {
        return new MeetingMinutes
        {
            Id = Guid.NewGuid(),
            MeetingJobId = jobId,
            Title = title,
            Summary = "Summary",
            DecisionsJson = "[]",
            ActionItemsJson = "[]",
            RisksJson = "[]",
            NextStepsJson = "[]",
            FullMinutesJson = "{\"title\":\"Minutes\"}",
            MinutesFilePath = "Minutes/minutes.md"
        };
    }
}

using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using MeetingMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

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
    public async Task ProcessingModeAndAudioDurationRoundTrip()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfMeetingJobRepository(dbContext);
        var job = CreateJob("transcript-only.mp3");
        job.ProcessingMode = MeetingProcessingMode.TranscriptOnly;
        job.SourceAudioDurationSeconds = 125;

        await repository.AddAsync(job, CancellationToken.None);

        var saved = await repository.GetByIdAsync(job.Id, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(MeetingProcessingMode.TranscriptOnly, saved.ProcessingMode);
        Assert.Equal(125, saved.SourceAudioDurationSeconds);
    }

    [Fact]
    public async Task GeneratedActionSeedingAndLegacyBackfillAreIdempotent()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var job = CreateJob("actions.mp3");
        job.Status = MeetingJobStatus.Completed;
        job.Stage = MeetingJobStage.Completed;
        job.Minutes = new MeetingMinutes
        {
            Id = Guid.NewGuid(), MeetingJobId = job.Id, Title = "Action meeting", Summary = "Summary",
            DecisionsJson = "[]", ActionItemsJson = "[{\"description\":\"Ship release\",\"owner\":\"Alex\",\"dueDate\":\"2026-08-15\"},{\"description\":\"Follow up\",\"owner\":null,\"dueDate\":\"Friday\"}]",
            RisksJson = "[]", NextStepsJson = "[]", FullMinutesJson = "{}", CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.MeetingJobs.Add(job);
        await dbContext.SaveChangesAsync();
        var repository = new EfActionRepository(dbContext, TimeProvider.System);

        Assert.Equal(2, await repository.SeedGeneratedAsync(job.Id, CancellationToken.None));
        Assert.Equal(0, await repository.SeedGeneratedAsync(job.Id, CancellationToken.None));
        var actions = await dbContext.ActionItems.AsNoTracking().OrderBy(action => action.Description).ToArrayAsync();
        Assert.Equal(2, actions.Length);
        Assert.Null(actions.Single(action => action.Description == "Follow up").DueDate);
        Assert.Equal(new DateOnly(2026, 8, 15), actions.Single(action => action.Description == "Ship release").DueDate);
        Assert.Equal(2, actions.Select(action => action.GeneratedSourceKey).Distinct().Count());
        Assert.NotNull(await dbContext.MeetingMinutes.Where(minutes => minutes.MeetingJobId == job.Id).Select(minutes => minutes.ActionsSeededAt).SingleAsync());

        var backfill = await repository.BackfillAsync(100, CancellationToken.None);
        Assert.Equal(0, backfill.ProcessedMeetings);
        Assert.False(backfill.HasMore);
    }

    [Fact]
    public async Task DatabaseRejectsInvalidModeSpecificAudioMetadata()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var job = CreateJob("imported.txt");
        job.OriginalFilePath = "Transcript/imported.txt";
        job.ProcessingMode = MeetingProcessingMode.MinutesFromTranscript;
        job.ProcessedFilePath = "Audio/Processed/invalid.wav";
        dbContext.MeetingJobs.Add(job);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Phase2RowsMigrateToFullMeetingAndIndexesExist()
    {
        await using var dbContext = _fixture.CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260719045819_AddSafeErrorCode");
        var jobId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO "MeetingJobs"
                ("Id", "OriginalFileName", "OriginalFilePath", "Status", "Stage", "Progress",
                 "AutomaticRetryCount", "AutomaticRetryLimit", "CreatedAt", "UpdatedAt")
            VALUES
                ({{jobId}}, 'legacy.mp3', 'Audio/Original/legacy.mp3', 'Completed', 'Completed', 100,
                 0, 0, {{now}}, {{now}})
            """);

        await migrator.MigrateAsync();
        dbContext.ChangeTracker.Clear();

        var migrated = await dbContext.MeetingJobs.AsNoTracking().SingleAsync(job => job.Id == jobId);
        var indexes = await dbContext.Database.SqlQueryRaw<string>(
                "SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'MeetingJobs'")
            .ToArrayAsync();

        Assert.Equal(MeetingProcessingMode.FullMeeting, migrated.ProcessingMode);
        Assert.Null(migrated.SourceAudioDurationSeconds);
        Assert.Contains("IX_MeetingJobs_CreatedAt_Id", indexes);
        Assert.Contains("IX_MeetingJobs_Status_CreatedAt", indexes);
        Assert.Contains("IX_MeetingJobs_ProcessingMode_CreatedAt", indexes);
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
    public async Task StructuredTranscriptCheckpointRoundTripsAndLegacySaveClearsIt()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfMeetingJobRepository(dbContext);
        var job = CreateJob("structured.wav");
        await repository.AddAsync(job, CancellationToken.None);
        var checkpoint = new StructuredTranscriptCheckpoint(
            [new TranscriptionSegment(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), "Hello team.")],
            [new TranscriptParagraph("Hello team.", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4))],
            new TranscriptFormattingSnapshot(1.5, 300, 700, "v1"));

        await repository.SaveStructuredTranscriptAsync(
            job.Id,
            "Hello team.",
            "Transcript/structured.txt",
            checkpoint,
            CancellationToken.None);
        var saved = await repository.GetStructuredTranscriptCheckpointAsync(job.Id, CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(checkpoint.Formatting, saved.Formatting);
        Assert.Equal(checkpoint.Segments, saved.Segments);
        Assert.Equal(checkpoint.Paragraphs, saved.Paragraphs);

        await repository.SaveTranscriptAsync(
            job.Id,
            "Imported transcript",
            "Transcript/imported.txt",
            CancellationToken.None);

        Assert.Null(await repository.GetStructuredTranscriptCheckpointAsync(job.Id, CancellationToken.None));
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
    public async Task DashboardSummaryUsesAggregatesAndBoundsRecentItems()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var jobs = Enumerable.Range(0, 7)
            .Select(index => CreateJob($"meeting-{index}.mp3", now.AddMinutes(-index)))
            .ToArray();
        jobs[0].Status = MeetingJobStatus.Completed;
        jobs[0].Stage = MeetingJobStage.Completed;
        jobs[0].StartedAt = now.AddSeconds(-120);
        jobs[0].CompletedAt = now.AddSeconds(-60);
        jobs[0].SourceAudioDurationSeconds = 90;
        jobs[1].Status = MeetingJobStatus.Failed;
        jobs[2].Status = MeetingJobStatus.Cancelled;
        jobs[3].Status = MeetingJobStatus.Processing;
        jobs[4].ProcessingMode = MeetingProcessingMode.TranscriptOnly;
        jobs[4].SourceAudioDurationSeconds = 30;
        jobs[5].ProcessingMode = MeetingProcessingMode.MinutesFromTranscript;
        jobs[5].OriginalFilePath = "Transcript/imported.txt";
        jobs[6].Status = MeetingJobStatus.Completed;
        jobs[6].Stage = MeetingJobStage.Completed;
        jobs[6].StartedAt = now.AddSeconds(-20);
        jobs[6].CompletedAt = now.AddSeconds(-30);
        dbContext.MeetingJobs.AddRange(jobs);
        dbContext.MeetingTranscripts.Add(new MeetingTranscript
        {
            Id = Guid.NewGuid(),
            MeetingJobId = jobs[0].Id,
            TranscriptText = "Transcript",
            TranscriptFilePath = "Transcript/one.txt"
        });
        dbContext.MeetingMinutes.Add(new MeetingMinutes
        {
            Id = Guid.NewGuid(),
            MeetingJobId = jobs[0].Id,
            Title = "Newest minutes",
            Summary = "Summary",
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync();

        var result = await new EfDashboardRepository(dbContext)
            .GetSummaryAsync(20, CancellationToken.None);

        Assert.Equal(7, result.TotalJobs);
        Assert.Equal(2, result.CompletedJobs);
        Assert.Equal(1, result.FailedJobs);
        Assert.Equal(1, result.CancelledJobs);
        Assert.Equal(1, result.ProcessingJobs);
        Assert.Equal(5, result.FullMeetingJobs);
        Assert.Equal(1, result.TranscriptOnlyJobs);
        Assert.Equal(1, result.MinutesFromTranscriptJobs);
        Assert.Equal(120, result.TotalAudioDurationSeconds);
        Assert.Equal(60, result.AverageCompletedProcessingDurationSeconds);
        Assert.Equal(1, result.TranscriptCount);
        Assert.Equal(1, result.MinutesCount);
        Assert.Equal(5, result.RecentJobs.Count);
        Assert.Single(result.RecentMinutes);
        Assert.Equal(jobs[0].Id, result.RecentJobs[0].JobId);
        Assert.Equal("Newest minutes", result.RecentMinutes[0].Title);
    }

    [Fact]
    public async Task MinutesQueryReturnsOnlyPersistedMinutesNewestFirstAndBounded()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var withoutMinutes = CreateJob("transcript-only.wav", now.AddMinutes(1));
        withoutMinutes.ProcessingMode = MeetingProcessingMode.TranscriptOnly;
        var older = CreateJob("older.wav", now.AddMinutes(-2));
        var newer = CreateJob("notes.md", now);
        newer.OriginalFilePath = "Transcript/notes.md";
        newer.ProcessingMode = MeetingProcessingMode.MinutesFromTranscript;
        dbContext.MeetingJobs.AddRange(withoutMinutes, older, newer);
        var olderMinutes = CreateMinutes(older.Id, "Older"); olderMinutes.CreatedAt = now.AddMinutes(-1);
        var newerMinutes = CreateMinutes(newer.Id, "Newer"); newerMinutes.CreatedAt = now;
        dbContext.MeetingMinutes.AddRange(olderMinutes, newerMinutes);
        await dbContext.SaveChangesAsync();

        var repository = new EfMeetingMinutesQueryRepository(dbContext);
        var page = await repository.GetPageAsync(0, 1, CancellationToken.None);

        Assert.Equal(2, await repository.CountAsync(CancellationToken.None));
        var item = Assert.Single(page);
        Assert.Equal(newer.Id, item.JobId);
        Assert.Equal("Transcript", item.SourceType);
        Assert.Equal("MinutesFromTranscript", item.ProcessingMode);
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
    public async Task ResetForRetryUsesModeSpecificInitialStage()
    {
        await _fixture.ResetAsync();
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfMeetingJobRepository(dbContext);
        var job = CreateJob("imported.txt");
        job.OriginalFilePath = "Transcript/imported.txt";
        job.ProcessingMode = MeetingProcessingMode.MinutesFromTranscript;
        job.Status = MeetingJobStatus.Failed;
        job.Stage = MeetingJobStage.GeneratingMinutes;
        await repository.AddAsync(job, CancellationToken.None);

        await repository.ResetForRetryAsync(job.Id, CancellationToken.None);

        var reset = await repository.GetByIdAsync(job.Id, CancellationToken.None);
        Assert.NotNull(reset);
        Assert.Equal(MeetingJobStage.GeneratingMinutes, reset.Stage);
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

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
    public async Task ResetForRetryClearsExecutionStateAndKeepsIdentity()
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
        job.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        job.CompletedAt = DateTimeOffset.UtcNow;
        await repository.AddAsync(job, CancellationToken.None);

        await repository.ResetForRetryAsync(job.Id, CancellationToken.None);
        var reset = await repository.GetByIdAsync(job.Id, CancellationToken.None);

        Assert.NotNull(reset);
        Assert.Equal(job.Id, reset.Id);
        Assert.Equal(MeetingJobStatus.Queued, reset.Status);
        Assert.Equal(MeetingJobStage.Uploaded, reset.Stage);
        Assert.Equal(0, reset.Progress);
        Assert.Null(reset.ErrorMessage);
        Assert.Null(reset.HangfireJobId);
        Assert.Null(reset.StartedAt);
        Assert.Null(reset.CompletedAt);
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

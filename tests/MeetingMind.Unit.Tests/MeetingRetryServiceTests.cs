using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Unit.Tests;

public class MeetingRetryServiceTests
{
    [Fact]
    public async Task RetryAsyncRequeuesFailedJob()
    {
        var jobId = Guid.NewGuid();
        var repository = new RetryStubMeetingJobRepository
        {
            MeetingJob = new MeetingJob
            {
                Id = jobId,
                Status = MeetingJobStatus.Failed,
                Stage = MeetingJobStage.Transcribing,
                Progress = 25,
                ErrorMessage = "Failed",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                CompletedAt = DateTimeOffset.UtcNow
            }
        };
        var backgroundJobs = new StubBackgroundJobService();
        var service = new MeetingRetryService(repository, backgroundJobs);

        var result = await service.RetryAsync(jobId, CancellationToken.None);

        Assert.Null(result.FailureReason);
        Assert.Equal("Queued", result.Status);
        Assert.Equal("Uploaded", result.Stage);
        Assert.True(repository.ResetForRetryWasCalled);
        Assert.Equal(jobId, backgroundJobs.EnqueuedJobId);
        Assert.Equal("hangfire-retry-id", repository.HangfireJobId);
    }

    [Fact]
    public async Task RetryAsyncUsesMinutesStageForTranscriptInput()
    {
        var jobId = Guid.NewGuid();
        var repository = new RetryStubMeetingJobRepository
        {
            MeetingJob = new MeetingJob
            {
                Id = jobId,
                ProcessingMode = MeetingProcessingMode.MinutesFromTranscript,
                Status = MeetingJobStatus.Failed,
                Stage = MeetingJobStage.GeneratingMinutes
            }
        };
        var service = new MeetingRetryService(repository, new StubBackgroundJobService());

        var result = await service.RetryAsync(jobId, CancellationToken.None);

        Assert.Equal("GeneratingMinutes", result.Stage);
    }

    [Fact]
    public async Task RetryAsyncRejectsCompletedJob()
    {
        var jobId = Guid.NewGuid();
        var repository = new RetryStubMeetingJobRepository
        {
            MeetingJob = new MeetingJob
            {
                Id = jobId,
                Status = MeetingJobStatus.Completed,
                Stage = MeetingJobStage.Completed
            }
        };
        var service = new MeetingRetryService(repository, new StubBackgroundJobService());

        var result = await service.RetryAsync(jobId, CancellationToken.None);

        Assert.Equal(MeetingRetryFailureReason.NotRetryable, result.FailureReason);
        Assert.False(repository.ResetForRetryWasCalled);
    }

    private sealed class StubBackgroundJobService : IBackgroundJobService
    {
        public Guid? EnqueuedJobId { get; private set; }

        public string EnqueueMeetingProcessing(Guid jobId)
        {
            EnqueuedJobId = jobId;
            return "hangfire-retry-id";
        }
    }
}

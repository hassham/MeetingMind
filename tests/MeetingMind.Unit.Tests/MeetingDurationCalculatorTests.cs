using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Unit.Tests;

public sealed class MeetingDurationCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InitialQueuedJobHasZeroProcessingAndLiveTotalDuration()
    {
        var job = CreateJob(MeetingJobStatus.Queued, createdAt: Now.AddSeconds(-90));

        var duration = MeetingDurationCalculator.Calculate(job, Now);

        Assert.Equal(0, duration.ProcessingDurationSeconds);
        Assert.Equal(90, duration.TotalDurationSeconds);
    }

    [Fact]
    public void ProcessingJobUsesCurrentTimeForBothDurations()
    {
        var job = CreateJob(MeetingJobStatus.Processing, createdAt: Now.AddSeconds(-120));
        job.StartedAt = Now.AddSeconds(-45);

        var duration = MeetingDurationCalculator.Calculate(job, Now);

        Assert.Equal(45, duration.ProcessingDurationSeconds);
        Assert.Equal(120, duration.TotalDurationSeconds);
    }

    [Fact]
    public void CompletedJobUsesCompletionTime()
    {
        var job = CreateTerminalJob(MeetingJobStatus.Completed);

        var duration = MeetingDurationCalculator.Calculate(job, Now);

        Assert.Equal(60, duration.ProcessingDurationSeconds);
        Assert.Equal(100, duration.TotalDurationSeconds);
    }

    [Fact]
    public void FailedJobWithoutCompletionUsesUpdatedTime()
    {
        var job = CreateJob(MeetingJobStatus.Failed, createdAt: Now.AddSeconds(-100));
        job.StartedAt = Now.AddSeconds(-50);
        job.UpdatedAt = Now.AddSeconds(-10);

        var duration = MeetingDurationCalculator.Calculate(job, Now);

        Assert.Equal(40, duration.ProcessingDurationSeconds);
        Assert.Equal(90, duration.TotalDurationSeconds);
    }

    [Fact]
    public void CancelledJobWithoutStartHasZeroProcessingDuration()
    {
        var job = CreateJob(MeetingJobStatus.Cancelled, createdAt: Now.AddSeconds(-30));
        job.CompletedAt = Now.AddSeconds(-5);

        var duration = MeetingDurationCalculator.Calculate(job, Now);

        Assert.Equal(0, duration.ProcessingDurationSeconds);
        Assert.Equal(25, duration.TotalDurationSeconds);
    }

    [Fact]
    public void RetriedQueuedJobKeepsPreviousProcessingAndLiveTotalDuration()
    {
        var job = CreateJob(MeetingJobStatus.Queued, createdAt: Now.AddSeconds(-200));
        job.StartedAt = Now.AddSeconds(-100);
        job.CompletedAt = Now.AddSeconds(-40);

        var duration = MeetingDurationCalculator.Calculate(job, Now);

        Assert.Equal(60, duration.ProcessingDurationSeconds);
        Assert.Equal(200, duration.TotalDurationSeconds);
    }

    [Fact]
    public void QueuedAutomaticRetryKeepsProcessingDurationLiveDuringBackoff()
    {
        var job = CreateJob(MeetingJobStatus.Queued, createdAt: Now.AddSeconds(-200));
        job.StartedAt = Now.AddSeconds(-100);
        job.AutomaticRetryCount = 1;
        job.AutomaticRetryLimit = 2;
        job.NextRetryAt = Now.AddSeconds(10);

        var duration = MeetingDurationCalculator.Calculate(job, Now);

        Assert.Equal(100, duration.ProcessingDurationSeconds);
        Assert.Equal(200, duration.TotalDurationSeconds);
    }

    [Fact]
    public void InvalidTimestampOrderIsClampedToZero()
    {
        var job = CreateJob(MeetingJobStatus.Completed, createdAt: Now);
        job.StartedAt = Now;
        job.CompletedAt = Now.AddSeconds(-10);

        var duration = MeetingDurationCalculator.Calculate(job, Now);

        Assert.Equal(0, duration.ProcessingDurationSeconds);
        Assert.Equal(0, duration.TotalDurationSeconds);
    }

    private static MeetingJob CreateTerminalJob(MeetingJobStatus status)
    {
        var job = CreateJob(status, createdAt: Now.AddSeconds(-100));
        job.StartedAt = Now.AddSeconds(-60);
        job.CompletedAt = Now;
        return job;
    }

    private static MeetingJob CreateJob(MeetingJobStatus status, DateTimeOffset createdAt)
    {
        return new MeetingJob
        {
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }
}

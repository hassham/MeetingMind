using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Unit.Tests;

public class MeetingStatusServiceTests
{
    [Fact]
    public async Task GetStatusAsyncMapsMeetingJobToStatusResult()
    {
        var jobId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero);
        var repository = new StubMeetingJobRepository
        {
            MeetingJob = new MeetingJob
            {
                Id = jobId,
                ProcessingMode = MeetingProcessingMode.TranscriptOnly,
                SourceAudioDurationSeconds = 75,
                Status = MeetingJobStatus.Failed,
                Stage = MeetingJobStage.Failed,
                Progress = 25,
                ErrorMessage = "Processing not yet implemented",
                AutomaticRetryCount = 1,
                AutomaticRetryLimit = 2,
                NextRetryAt = now.AddSeconds(30),
                CreatedAt = now.AddSeconds(-100),
                StartedAt = now.AddSeconds(-60),
                CompletedAt = now.AddSeconds(-10),
                UpdatedAt = now.AddSeconds(-10)
            }
        };
        var service = new MeetingStatusService(repository, new FixedTimeProvider(now));

        var result = await service.GetStatusAsync(jobId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal("TranscriptOnly", result.ProcessingMode);
        Assert.Equal(75, result.SourceAudioDurationSeconds);
        Assert.Equal("Failed", result.Status);
        Assert.Equal("Failed", result.Stage);
        Assert.Equal(25, result.Progress);
        Assert.Equal("Processing not yet implemented", result.ErrorMessage);
        Assert.Equal(1, result.AutomaticRetryCount);
        Assert.Equal(2, result.AutomaticRetryLimit);
        Assert.Equal(now.AddSeconds(30), result.NextRetryAt);
        Assert.Equal(50, result.ProcessingDurationSeconds);
        Assert.Equal(90, result.TotalDurationSeconds);
    }

    [Fact]
    public async Task GetStatusAsyncReturnsNullForMissingMeetingJob()
    {
        var service = new MeetingStatusService(
            new StubMeetingJobRepository(),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.GetStatusAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class StubMeetingJobRepository : IMeetingJobRepository
    {
        public MeetingJob? MeetingJob { get; init; }

        public Task AddAsync(MeetingJob meetingJob, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<MeetingJob?> GetByIdAsync(Guid meetingJobId, CancellationToken cancellationToken)
        {
            return Task.FromResult(MeetingJob?.Id == meetingJobId ? MeetingJob : null);
        }

        public Task<MeetingTranscript?> GetTranscriptByJobIdAsync(
            Guid meetingJobId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<MeetingMinutes?> GetMinutesByJobIdAsync(
            Guid meetingJobId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MeetingJob>> GetHistoryAsync(
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetHangfireJobIdAsync(Guid meetingJobId, string hangfireJobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetProcessedFilePathAsync(
            Guid meetingJobId,
            string processedFilePath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SaveTranscriptAsync(
            Guid meetingJobId,
            string transcriptText,
            string transcriptFilePath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SaveMinutesAsync(
            Guid meetingJobId,
            MeetingMinutes minutes,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ResetForRetryAsync(Guid meetingJobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task BeginProcessingAsync(Guid meetingJobId, int automaticRetryLimit, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ScheduleAutomaticRetryAsync(
            Guid meetingJobId,
            MeetingJobStage stage,
            int progress,
            string errorCode,
            string errorMessage,
            int automaticRetryCount,
            int automaticRetryLimit,
            DateTimeOffset nextRetryAt,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task RecordFinalFailureAsync(
            Guid meetingJobId,
            MeetingJobStage stage,
            int progress,
            string errorCode,
            string errorMessage,
            int automaticRetryCount,
            int automaticRetryLimit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task UpdateStatusAsync(
            Guid meetingJobId,
            MeetingJobStatus status,
            MeetingJobStage stage,
            int progress,
            string? errorMessage,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}

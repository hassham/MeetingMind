using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Domain.Tests;

public class MeetingStatusServiceTests
{
    [Fact]
    public async Task GetStatusAsyncMapsMeetingJobToStatusResult()
    {
        var jobId = Guid.NewGuid();
        var repository = new StubMeetingJobRepository
        {
            MeetingJob = new MeetingJob
            {
                Id = jobId,
                Status = MeetingJobStatus.Failed,
                Stage = MeetingJobStage.Failed,
                Progress = 25,
                ErrorMessage = "Processing not yet implemented"
            }
        };
        var service = new MeetingStatusService(repository);

        var result = await service.GetStatusAsync(jobId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal("Failed", result.Status);
        Assert.Equal("Failed", result.Stage);
        Assert.Equal(25, result.Progress);
        Assert.Equal("Processing not yet implemented", result.ErrorMessage);
    }

    [Fact]
    public async Task GetStatusAsyncReturnsNullForMissingMeetingJob()
    {
        var service = new MeetingStatusService(new StubMeetingJobRepository());

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

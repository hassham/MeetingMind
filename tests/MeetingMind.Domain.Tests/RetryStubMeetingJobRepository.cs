using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Domain.Tests;

internal sealed class RetryStubMeetingJobRepository : IMeetingJobRepository
{
    public MeetingJob? MeetingJob { get; init; }

    public IReadOnlyList<MeetingJob> History { get; init; } = Array.Empty<MeetingJob>();

    public int Total { get; init; }

    public bool ResetForRetryWasCalled { get; private set; }

    public string? HangfireJobId { get; private set; }

    public Task AddAsync(MeetingJob meetingJob, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<MeetingJob?> GetByIdAsync(Guid meetingJobId, CancellationToken cancellationToken)
    {
        return Task.FromResult(MeetingJob?.Id == meetingJobId ? MeetingJob : null);
    }

    public Task<MeetingTranscript?> GetTranscriptByJobIdAsync(Guid meetingJobId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<MeetingMinutes?> GetMinutesByJobIdAsync(Guid meetingJobId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<MeetingJob>> GetHistoryAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(History);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Total);
    }

    public Task SetHangfireJobIdAsync(Guid meetingJobId, string hangfireJobId, CancellationToken cancellationToken)
    {
        HangfireJobId = hangfireJobId;
        return Task.CompletedTask;
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

    public Task SaveMinutesAsync(Guid meetingJobId, MeetingMinutes minutes, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task ResetForRetryAsync(Guid meetingJobId, CancellationToken cancellationToken)
    {
        ResetForRetryWasCalled = true;
        return Task.CompletedTask;
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

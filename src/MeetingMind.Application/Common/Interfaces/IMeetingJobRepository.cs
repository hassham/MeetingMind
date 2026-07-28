using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using MeetingMind.Application.Meetings;

namespace MeetingMind.Application.Common.Interfaces;

public interface IMeetingJobRepository
{
    Task AddAsync(MeetingJob meetingJob, CancellationToken cancellationToken);

    Task<MeetingJob?> GetByIdAsync(Guid meetingJobId, CancellationToken cancellationToken);

    Task<MeetingTranscript?> GetTranscriptByJobIdAsync(Guid meetingJobId, CancellationToken cancellationToken);

    Task<StructuredTranscriptCheckpoint?> GetStructuredTranscriptCheckpointAsync(
        Guid meetingJobId,
        CancellationToken cancellationToken) => Task.FromResult<StructuredTranscriptCheckpoint?>(null);

    Task<MeetingMinutes?> GetMinutesByJobIdAsync(Guid meetingJobId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MeetingJob>> GetHistoryAsync(int skip, int take, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    Task SetHangfireJobIdAsync(Guid meetingJobId, string hangfireJobId, CancellationToken cancellationToken);

    Task SetProcessedFilePathAsync(Guid meetingJobId, string processedFilePath, CancellationToken cancellationToken);

    Task SetAudioProcessingResultAsync(
        Guid meetingJobId,
        string processedFilePath,
        long sourceAudioDurationSeconds,
        CancellationToken cancellationToken);

    Task SaveTranscriptAsync(
        Guid meetingJobId,
        string transcriptText,
        string transcriptFilePath,
        CancellationToken cancellationToken);

    Task SaveStructuredTranscriptAsync(
        Guid meetingJobId,
        string transcriptText,
        string transcriptFilePath,
        StructuredTranscriptCheckpoint checkpoint,
        CancellationToken cancellationToken) =>
        SaveTranscriptAsync(
            meetingJobId,
            transcriptText,
            transcriptFilePath,
            cancellationToken);

    Task SaveMinutesAsync(
        Guid meetingJobId,
        MeetingMinutes minutes,
        CancellationToken cancellationToken);

    Task ResetForRetryAsync(Guid meetingJobId, CancellationToken cancellationToken);

    Task BeginProcessingAsync(
        Guid meetingJobId,
        int automaticRetryLimit,
        CancellationToken cancellationToken);

    Task ScheduleAutomaticRetryAsync(
        Guid meetingJobId,
        MeetingJobStage stage,
        int progress,
        string errorCode,
        string errorMessage,
        int automaticRetryCount,
        int automaticRetryLimit,
        DateTimeOffset nextRetryAt,
        CancellationToken cancellationToken);

    Task RecordFinalFailureAsync(
        Guid meetingJobId,
        MeetingJobStage stage,
        int progress,
        string errorCode,
        string errorMessage,
        int automaticRetryCount,
        int automaticRetryLimit,
        CancellationToken cancellationToken);

    Task UpdateStatusAsync(
        Guid meetingJobId,
        MeetingJobStatus status,
        MeetingJobStage stage,
        int progress,
        string? errorMessage,
        CancellationToken cancellationToken);
}

using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Application.Common.Interfaces;

public interface IMeetingJobRepository
{
    Task AddAsync(MeetingJob meetingJob, CancellationToken cancellationToken);

    Task<MeetingJob?> GetByIdAsync(Guid meetingJobId, CancellationToken cancellationToken);

    Task<MeetingTranscript?> GetTranscriptByJobIdAsync(Guid meetingJobId, CancellationToken cancellationToken);

    Task SetHangfireJobIdAsync(Guid meetingJobId, string hangfireJobId, CancellationToken cancellationToken);

    Task SetProcessedFilePathAsync(Guid meetingJobId, string processedFilePath, CancellationToken cancellationToken);

    Task SaveTranscriptAsync(
        Guid meetingJobId,
        string transcriptText,
        string transcriptFilePath,
        CancellationToken cancellationToken);

    Task UpdateStatusAsync(
        Guid meetingJobId,
        MeetingJobStatus status,
        MeetingJobStage stage,
        int progress,
        string? errorMessage,
        CancellationToken cancellationToken);
}

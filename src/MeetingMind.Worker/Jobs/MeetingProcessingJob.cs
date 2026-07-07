using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Worker.Jobs;

public class MeetingProcessingJob : IMeetingProcessingJob
{
    private readonly ILogger<MeetingProcessingJob> _logger;
    private readonly IMeetingJobRepository _meetingJobRepository;

    public MeetingProcessingJob(
        ILogger<MeetingProcessingJob> logger,
        IMeetingJobRepository meetingJobRepository)
    {
        _logger = logger;
        _meetingJobRepository = meetingJobRepository;
    }

    public async Task ProcessMeetingAsync(Guid jobId)
    {
        _logger.LogInformation("Meeting processing started for job {JobId}", jobId);

        await _meetingJobRepository.UpdateStatusAsync(
            jobId,
            MeetingJobStatus.Processing,
            MeetingJobStage.Validating,
            progress: 0,
            errorMessage: null,
            CancellationToken.None);

        await _meetingJobRepository.UpdateStatusAsync(
            jobId,
            MeetingJobStatus.Failed,
            MeetingJobStage.Failed,
            progress: 0,
            errorMessage: "Processing not yet implemented",
            CancellationToken.None);
    }
}

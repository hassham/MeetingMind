using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Enums;
using MeetingMind.Worker.Options;

namespace MeetingMind.Worker.Jobs;

public class MeetingProcessingJob : IMeetingProcessingJob
{
    private readonly ILogger<MeetingProcessingJob> _logger;
    private readonly IMeetingJobRepository _meetingJobRepository;
    private readonly ProcessingOptions _processingOptions;

    public MeetingProcessingJob(
        ILogger<MeetingProcessingJob> logger,
        IMeetingJobRepository meetingJobRepository,
        ProcessingOptions processingOptions)
    {
        _logger = logger;
        _meetingJobRepository = meetingJobRepository;
        _processingOptions = processingOptions;
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

        if (_processingOptions.StubProcessingDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(_processingOptions.StubProcessingDelaySeconds));
        }

        await _meetingJobRepository.UpdateStatusAsync(
            jobId,
            MeetingJobStatus.Failed,
            MeetingJobStage.Failed,
            progress: 0,
            errorMessage: "Processing not yet implemented",
            CancellationToken.None);
    }
}

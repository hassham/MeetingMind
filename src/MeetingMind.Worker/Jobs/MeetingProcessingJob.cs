using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Enums;
using MeetingMind.Worker.Options;

namespace MeetingMind.Worker.Jobs;

public class MeetingProcessingJob : IMeetingProcessingJob
{
    private const int MaxErrorLength = 1000;

    private readonly ILogger<MeetingProcessingJob> _logger;
    private readonly IAudioProcessingService _audioProcessingService;
    private readonly IMeetingJobRepository _meetingJobRepository;
    private readonly ProcessingOptions _processingOptions;

    public MeetingProcessingJob(
        ILogger<MeetingProcessingJob> logger,
        IAudioProcessingService audioProcessingService,
        IMeetingJobRepository meetingJobRepository,
        ProcessingOptions processingOptions)
    {
        _logger = logger;
        _audioProcessingService = audioProcessingService;
        _meetingJobRepository = meetingJobRepository;
        _processingOptions = processingOptions;
    }

    public async Task ProcessMeetingAsync(Guid jobId)
    {
        _logger.LogInformation("Meeting processing started for job {JobId}", jobId);

        try
        {
            var meetingJob = await _meetingJobRepository.GetByIdAsync(jobId, CancellationToken.None);
            if (meetingJob is null)
            {
                _logger.LogWarning("Meeting processing skipped because job {JobId} was not found", jobId);
                return;
            }

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
                MeetingJobStatus.Processing,
                MeetingJobStage.Transcoding,
                progress: 10,
                errorMessage: null,
                CancellationToken.None);

            var processedFilePath = await _audioProcessingService.ConvertToStandardFormatAsync(
                meetingJob.OriginalFilePath,
                CancellationToken.None);

            await _meetingJobRepository.SetProcessedFilePathAsync(
                jobId,
                processedFilePath,
                CancellationToken.None);

            _logger.LogInformation(
                "Audio processing completed for job {JobId}; processed file path saved",
                jobId);

            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Failed,
                MeetingJobStage.Transcribing,
                progress: 25,
                errorMessage: "Transcription not yet implemented",
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Meeting processing failed for job {JobId}", jobId);

            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Failed,
                MeetingJobStage.Transcoding,
                progress: 10,
                errorMessage: SanitizeError(exception.Message),
                CancellationToken.None);
        }
    }

    private static string SanitizeError(string message)
    {
        var sanitized = message
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        if (sanitized.Length <= MaxErrorLength)
        {
            return sanitized;
        }

        return sanitized[..MaxErrorLength];
    }
}

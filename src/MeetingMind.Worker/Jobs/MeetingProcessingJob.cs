using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Enums;
using MeetingMind.Worker.Options;

namespace MeetingMind.Worker.Jobs;

public class MeetingProcessingJob : IMeetingProcessingJob
{
    private const int MaxErrorLength = 1000;

    private readonly ILogger<MeetingProcessingJob> _logger;
    private readonly IAudioProcessingService _audioProcessingService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMeetingJobRepository _meetingJobRepository;
    private readonly ProcessingOptions _processingOptions;
    private readonly ITranscriptionService _transcriptionService;

    public MeetingProcessingJob(
        ILogger<MeetingProcessingJob> logger,
        IAudioProcessingService audioProcessingService,
        IFileStorageService fileStorageService,
        IMeetingJobRepository meetingJobRepository,
        ProcessingOptions processingOptions,
        ITranscriptionService transcriptionService)
    {
        _logger = logger;
        _audioProcessingService = audioProcessingService;
        _fileStorageService = fileStorageService;
        _meetingJobRepository = meetingJobRepository;
        _processingOptions = processingOptions;
        _transcriptionService = transcriptionService;
    }

    public async Task ProcessMeetingAsync(Guid jobId)
    {
        _logger.LogInformation("Meeting processing started for job {JobId}", jobId);
        var currentStage = MeetingJobStage.Transcoding;
        var currentProgress = 10;

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
            currentStage = MeetingJobStage.Validating;
            currentProgress = 0;

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
            currentStage = MeetingJobStage.Transcoding;
            currentProgress = 10;

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
                MeetingJobStatus.Processing,
                MeetingJobStage.Transcribing,
                progress: 25,
                errorMessage: null,
                CancellationToken.None);
            currentStage = MeetingJobStage.Transcribing;
            currentProgress = 25;

            var transcriptText = await _transcriptionService.TranscribeAsync(
                processedFilePath,
                CancellationToken.None);

            var transcriptFilePath = await _fileStorageService.SaveTranscriptAsync(
                jobId,
                transcriptText,
                CancellationToken.None);

            await _meetingJobRepository.SaveTranscriptAsync(
                jobId,
                transcriptText,
                transcriptFilePath,
                CancellationToken.None);

            _logger.LogInformation(
                "Transcription completed for job {JobId}; transcript file path saved",
                jobId);

            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Failed,
                MeetingJobStage.GeneratingMinutes,
                progress: 60,
                errorMessage: "Meeting minutes not yet implemented",
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Meeting processing failed for job {JobId}", jobId);

            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Failed,
                currentStage,
                currentProgress,
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

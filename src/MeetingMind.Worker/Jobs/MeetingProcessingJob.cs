using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using MeetingMind.Worker.Options;
using System.Text.Json;

namespace MeetingMind.Worker.Jobs;

public class MeetingProcessingJob : IMeetingProcessingJob
{
    private const int MaxErrorLength = 1000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<MeetingProcessingJob> _logger;
    private readonly IAudioProcessingService _audioProcessingService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMeetingJobRepository _meetingJobRepository;
    private readonly ProcessingOptions _processingOptions;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IMeetingMinutesService _meetingMinutesService;

    public MeetingProcessingJob(
        ILogger<MeetingProcessingJob> logger,
        IAudioProcessingService audioProcessingService,
        IFileStorageService fileStorageService,
        IMeetingJobRepository meetingJobRepository,
        ProcessingOptions processingOptions,
        ITranscriptionService transcriptionService,
        IMeetingMinutesService meetingMinutesService)
    {
        _logger = logger;
        _audioProcessingService = audioProcessingService;
        _fileStorageService = fileStorageService;
        _meetingJobRepository = meetingJobRepository;
        _processingOptions = processingOptions;
        _transcriptionService = transcriptionService;
        _meetingMinutesService = meetingMinutesService;
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
                MeetingJobStatus.Processing,
                MeetingJobStage.GeneratingMinutes,
                progress: 60,
                errorMessage: null,
                CancellationToken.None);
            currentStage = MeetingJobStage.GeneratingMinutes;
            currentProgress = 60;

            var generatedMinutes = await _meetingMinutesService.GenerateMinutesAsync(
                transcriptText,
                CancellationToken.None);

            var minutesMarkdown = MeetingMinutesFormatter.ToMarkdown(generatedMinutes);
            var minutesFilePath = await _fileStorageService.SaveMinutesAsync(
                jobId,
                minutesMarkdown,
                CancellationToken.None);

            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Processing,
                MeetingJobStage.SavingResults,
                progress: 90,
                errorMessage: null,
                CancellationToken.None);
            currentStage = MeetingJobStage.SavingResults;
            currentProgress = 90;

            await _meetingJobRepository.SaveMinutesAsync(
                jobId,
                CreateMeetingMinutes(jobId, generatedMinutes, minutesFilePath),
                CancellationToken.None);

            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Completed,
                MeetingJobStage.Completed,
                progress: 100,
                errorMessage: null,
                CancellationToken.None);

            _logger.LogInformation("Meeting processing completed for job {JobId}", jobId);
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

    private static MeetingMinutes CreateMeetingMinutes(
        Guid jobId,
        MeetingMinutesContent minutes,
        string minutesFilePath)
    {
        return new MeetingMinutes
        {
            Id = Guid.NewGuid(),
            MeetingJobId = jobId,
            Title = minutes.Title,
            Summary = minutes.Summary,
            DecisionsJson = JsonSerializer.Serialize(minutes.Decisions, JsonOptions),
            ActionItemsJson = JsonSerializer.Serialize(minutes.ActionItems, JsonOptions),
            RisksJson = JsonSerializer.Serialize(minutes.Risks, JsonOptions),
            NextStepsJson = JsonSerializer.Serialize(minutes.NextSteps, JsonOptions),
            FullMinutesJson = JsonSerializer.Serialize(minutes, JsonOptions),
            MinutesFilePath = minutesFilePath,
            CreatedAt = DateTimeOffset.UtcNow
        };
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

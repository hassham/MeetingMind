using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Failures;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using System.Text.Json;

namespace MeetingMind.Worker.Jobs;

public class MeetingProcessingJob : IMeetingProcessingJob
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<MeetingProcessingJob> _logger;
    private readonly IAudioProcessingService _audioProcessingService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMeetingJobRepository _meetingJobRepository;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IMeetingMinutesService _meetingMinutesService;
    private readonly IMeetingFailureClassifier _failureClassifier;
    private readonly AutomaticRetryOptions _retryOptions;
    private readonly TimeProvider _timeProvider;

    public MeetingProcessingJob(
        ILogger<MeetingProcessingJob> logger,
        IAudioProcessingService audioProcessingService,
        IFileStorageService fileStorageService,
        IMeetingJobRepository meetingJobRepository,
        ITranscriptionService transcriptionService,
        IMeetingMinutesService meetingMinutesService,
        IMeetingFailureClassifier failureClassifier,
        AutomaticRetryOptions retryOptions,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _audioProcessingService = audioProcessingService;
        _fileStorageService = fileStorageService;
        _meetingJobRepository = meetingJobRepository;
        _transcriptionService = transcriptionService;
        _meetingMinutesService = meetingMinutesService;
        _failureClassifier = failureClassifier;
        _retryOptions = retryOptions;
        _timeProvider = timeProvider;
    }

    public async Task ProcessMeetingAsync(Guid jobId)
    {
        _logger.LogInformation("Meeting processing started for job {JobId}", jobId);
        var currentStage = MeetingJobStage.Validating;
        var currentProgress = 0;
        MeetingJob? meetingJob = null;

        try
        {
            meetingJob = await _meetingJobRepository.GetByIdAsync(jobId, CancellationToken.None);
            if (meetingJob is null)
            {
                _logger.LogWarning("Meeting processing skipped because job {JobId} was not found", jobId);
                return;
            }

            await _meetingJobRepository.BeginProcessingAsync(
                jobId,
                _retryOptions.RetryLimit,
                CancellationToken.None);

            var transcript = await GetValidTranscriptCheckpointAsync(jobId);
            string transcriptText;

            if (transcript is not null)
            {
                transcriptText = transcript.TranscriptText;
                _logger.LogInformation(
                    "Meeting processing resumed from transcript checkpoint for job {JobId}",
                    jobId);
            }
            else
            {
                var processedFilePath = await GetOrCreateProcessedAudioAsync(
                    meetingJob,
                    jobId,
                    stage => currentStage = stage,
                    progress => currentProgress = progress);

                currentStage = MeetingJobStage.Transcribing;
                currentProgress = 25;
                await _meetingJobRepository.UpdateStatusAsync(
                    jobId,
                    MeetingJobStatus.Processing,
                    currentStage,
                    currentProgress,
                    errorMessage: null,
                    CancellationToken.None);

                transcriptText = await _transcriptionService.TranscribeAsync(
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
                    "Transcription completed for job {JobId}; transcript checkpoint saved",
                    jobId);
            }

            currentStage = MeetingJobStage.GeneratingMinutes;
            currentProgress = 60;
            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Processing,
                currentStage,
                currentProgress,
                errorMessage: null,
                CancellationToken.None);

            var generatedMinutes = await _meetingMinutesService.GenerateMinutesAsync(
                transcriptText,
                CancellationToken.None);

            var minutesMarkdown = MeetingMinutesFormatter.ToMarkdown(generatedMinutes);
            var minutesFilePath = await _fileStorageService.SaveMinutesAsync(
                jobId,
                minutesMarkdown,
                CancellationToken.None);

            currentStage = MeetingJobStage.SavingResults;
            currentProgress = 90;
            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Processing,
                currentStage,
                currentProgress,
                errorMessage: null,
                CancellationToken.None);

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

            _logger.LogInformation(
                "Meeting processing completed for job {JobId} after {AutomaticRetryCount} automatic retries",
                jobId,
                meetingJob.AutomaticRetryCount);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Meeting processing attempt failed for job {JobId} at stage {Stage}",
                jobId,
                currentStage);

            if (meetingJob is null)
            {
                throw;
            }

            var classification = _failureClassifier.Classify(exception);
            if (classification.Kind == MeetingFailureKind.Permanent)
            {
                await _meetingJobRepository.RecordFinalFailureAsync(
                    jobId,
                    currentStage,
                    currentProgress,
                    classification.SafeMessage,
                    meetingJob.AutomaticRetryCount,
                    _retryOptions.RetryLimit,
                    CancellationToken.None);

                throw exception as PermanentMeetingProcessingException ??
                      new PermanentMeetingProcessingException(classification.SafeMessage, exception);
            }

            if (meetingJob.AutomaticRetryCount < _retryOptions.RetryLimit)
            {
                var nextRetryCount = meetingJob.AutomaticRetryCount + 1;
                var delay = TimeSpan.FromSeconds(
                    _retryOptions.DelaysInSeconds[meetingJob.AutomaticRetryCount]);
                var nextRetryAt = _timeProvider.GetUtcNow().Add(delay);

                await _meetingJobRepository.ScheduleAutomaticRetryAsync(
                    jobId,
                    currentStage,
                    currentProgress,
                    classification.SafeMessage,
                    nextRetryCount,
                    _retryOptions.RetryLimit,
                    nextRetryAt,
                    CancellationToken.None);

                _logger.LogWarning(
                    "Automatic retry {AutomaticRetryCount} of {AutomaticRetryLimit} scheduled for job {JobId} at {NextRetryAt}",
                    nextRetryCount,
                    _retryOptions.RetryLimit,
                    jobId,
                    nextRetryAt);

                throw;
            }

            await _meetingJobRepository.RecordFinalFailureAsync(
                jobId,
                currentStage,
                currentProgress,
                classification.SafeMessage,
                meetingJob.AutomaticRetryCount,
                _retryOptions.RetryLimit,
                CancellationToken.None);

            throw;
        }
    }

    private async Task<MeetingTranscript?> GetValidTranscriptCheckpointAsync(Guid jobId)
    {
        var transcript = await _meetingJobRepository.GetTranscriptByJobIdAsync(
            jobId,
            CancellationToken.None);

        if (transcript is null ||
            string.IsNullOrWhiteSpace(transcript.TranscriptText) ||
            string.IsNullOrWhiteSpace(transcript.TranscriptFilePath))
        {
            return null;
        }

        return await _fileStorageService.ExistsAsync(
            transcript.TranscriptFilePath,
            CancellationToken.None)
            ? transcript
            : null;
    }

    private async Task<string> GetOrCreateProcessedAudioAsync(
        MeetingJob meetingJob,
        Guid jobId,
        Action<MeetingJobStage> setStage,
        Action<int> setProgress)
    {
        if (!string.IsNullOrWhiteSpace(meetingJob.ProcessedFilePath) &&
            await _fileStorageService.ExistsAsync(
                meetingJob.ProcessedFilePath,
                CancellationToken.None))
        {
            _logger.LogInformation(
                "Meeting processing resumed from processed-audio checkpoint for job {JobId}",
                jobId);
            return meetingJob.ProcessedFilePath;
        }

        setStage(MeetingJobStage.Transcoding);
        setProgress(10);
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
            "Audio processing completed for job {JobId}; processed-audio checkpoint saved",
            jobId);

        return processedFilePath;
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
}

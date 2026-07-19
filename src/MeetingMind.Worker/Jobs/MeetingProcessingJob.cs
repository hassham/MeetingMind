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
        var attemptStarted = _timeProvider.GetTimestamp();
        var stageStarted = attemptStarted;
        var currentStage = MeetingJobStage.Validating;
        var currentProgress = 0;
        var attemptNumber = 1;
        MeetingJob? meetingJob = null;

        try
        {
            meetingJob = await _meetingJobRepository.GetByIdAsync(jobId, CancellationToken.None);
            if (meetingJob is null)
            {
                _logger.LogWarning(
                    "Meeting processing event for job {JobId}; stage {Stage}; outcome {Outcome}; elapsed {ElapsedMilliseconds} ms",
                    jobId,
                    currentStage,
                    "NotFound",
                    GetElapsedMilliseconds(attemptStarted));
                return;
            }

            attemptNumber = meetingJob.AutomaticRetryCount + 1;
            LogStageOutcome(
                LogLevel.Information,
                jobId,
                currentStage,
                "Started",
                currentProgress,
                attemptNumber,
                attemptStarted);

            await _meetingJobRepository.BeginProcessingAsync(
                jobId,
                _retryOptions.RetryLimit,
                CancellationToken.None);

            LogStageOutcome(
                LogLevel.Information,
                jobId,
                MeetingJobStage.Validating,
                "Succeeded",
                currentProgress,
                attemptNumber,
                stageStarted);

            var transcript = await GetValidTranscriptCheckpointAsync(jobId);
            string transcriptText;

            if (transcript is not null)
            {
                transcriptText = transcript.TranscriptText;
                LogStageOutcome(
                    LogLevel.Information,
                    jobId,
                    MeetingJobStage.Transcribing,
                    "CheckpointReused",
                    60,
                    attemptNumber,
                    _timeProvider.GetTimestamp());
            }
            else
            {
                var processedFilePath = await GetOrCreateProcessedAudioAsync(
                    meetingJob,
                    jobId,
                    attemptNumber,
                    stage => currentStage = stage,
                    progress => currentProgress = progress);

                currentStage = MeetingJobStage.Transcribing;
                currentProgress = 25;
                stageStarted = _timeProvider.GetTimestamp();
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

                LogStageOutcome(
                    LogLevel.Information,
                    jobId,
                    currentStage,
                    "Succeeded",
                    60,
                    attemptNumber,
                    stageStarted);
            }

            currentStage = MeetingJobStage.GeneratingMinutes;
            currentProgress = 60;
            stageStarted = _timeProvider.GetTimestamp();
            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Processing,
                currentStage,
                currentProgress,
                errorMessage: null,
                CancellationToken.None);

            var generatedMinutes = await _meetingMinutesService.GenerateMinutesAsync(
                transcriptText,
                CancellationToken.None,
                async (progress, cancellationToken) =>
                {
                    currentProgress = progress.Percent;
                    await _meetingJobRepository.UpdateStatusAsync(
                        jobId,
                        MeetingJobStatus.Processing,
                        MeetingJobStage.GeneratingMinutes,
                        progress.Percent,
                        errorMessage: null,
                        cancellationToken);

                    _logger.LogDebug(
                        "Meeting processing progress for job {JobId}; stage {Stage}; phase {Phase}; completed {Completed}; total {Total}; progress {Progress}",
                        jobId,
                        MeetingJobStage.GeneratingMinutes,
                        progress.Phase,
                        progress.Completed,
                        progress.Total,
                        progress.Percent);
                });

            LogStageOutcome(
                LogLevel.Information,
                jobId,
                currentStage,
                "Succeeded",
                89,
                attemptNumber,
                stageStarted);

            var minutesMarkdown = MeetingMinutesFormatter.ToMarkdown(generatedMinutes);
            currentStage = MeetingJobStage.SavingResults;
            currentProgress = 90;
            stageStarted = _timeProvider.GetTimestamp();
            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Processing,
                currentStage,
                currentProgress,
                errorMessage: null,
                CancellationToken.None);

            var minutesFilePath = await _fileStorageService.SaveMinutesAsync(
                jobId,
                minutesMarkdown,
                CancellationToken.None);

            await _meetingJobRepository.SaveMinutesAsync(
                jobId,
                CreateMeetingMinutes(jobId, generatedMinutes, minutesFilePath),
                CancellationToken.None);

            LogStageOutcome(
                LogLevel.Information,
                jobId,
                currentStage,
                "Succeeded",
                currentProgress,
                attemptNumber,
                stageStarted);

            await _meetingJobRepository.UpdateStatusAsync(
                jobId,
                MeetingJobStatus.Completed,
                MeetingJobStage.Completed,
                progress: 100,
                errorMessage: null,
                CancellationToken.None);

            LogStageOutcome(
                LogLevel.Information,
                jobId,
                MeetingJobStage.Completed,
                "Succeeded",
                100,
                attemptNumber,
                attemptStarted);
        }
        catch (Exception exception)
        {
            var classification = _failureClassifier.Classify(exception);
            if (meetingJob is null)
            {
                _logger.LogError(
                    "Meeting processing event for job {JobId}; stage {Stage}; outcome {Outcome}; error code {ErrorCode}; classification {FailureKind}; exception {ExceptionType}; elapsed {ElapsedMilliseconds} ms",
                    jobId,
                    currentStage,
                    "FailedBeforeLoad",
                    classification.ErrorCode,
                    classification.Kind,
                    exception.GetType().Name,
                    GetElapsedMilliseconds(stageStarted));
                throw CreateSafeException(classification);
            }

            _logger.LogError(
                "Meeting processing event for job {JobId}; stage {Stage}; outcome {Outcome}; attempt {Attempt}; progress {Progress}; error code {ErrorCode}; classification {FailureKind}; exception {ExceptionType}; elapsed {ElapsedMilliseconds} ms",
                jobId,
                currentStage,
                classification.Kind == MeetingFailureKind.Permanent ? "Failed" : "AttemptFailed",
                attemptNumber,
                currentProgress,
                classification.ErrorCode,
                classification.Kind,
                exception.GetType().Name,
                GetElapsedMilliseconds(stageStarted));
            if (classification.Kind == MeetingFailureKind.Permanent)
            {
                await PersistFailureStateAsync(
                    () => _meetingJobRepository.RecordFinalFailureAsync(
                        jobId,
                        currentStage,
                        currentProgress,
                        classification.ErrorCode,
                        classification.SafeMessage,
                        meetingJob.AutomaticRetryCount,
                        _retryOptions.RetryLimit,
                        CancellationToken.None),
                    jobId,
                    currentStage,
                    stageStarted);

                throw CreateSafeException(classification);
            }

            if (meetingJob.AutomaticRetryCount < _retryOptions.RetryLimit)
            {
                var nextRetryCount = meetingJob.AutomaticRetryCount + 1;
                var delay = TimeSpan.FromSeconds(
                    _retryOptions.DelaysInSeconds[meetingJob.AutomaticRetryCount]);
                var nextRetryAt = _timeProvider.GetUtcNow().Add(delay);

                await PersistFailureStateAsync(
                    () => _meetingJobRepository.ScheduleAutomaticRetryAsync(
                        jobId,
                        currentStage,
                        currentProgress,
                        classification.ErrorCode,
                        classification.SafeMessage,
                        nextRetryCount,
                        _retryOptions.RetryLimit,
                        nextRetryAt,
                        CancellationToken.None),
                    jobId,
                    currentStage,
                    stageStarted);

                _logger.LogWarning(
                    "Meeting processing event for job {JobId}; stage {Stage}; outcome {Outcome}; attempt {Attempt}; progress {Progress}; error code {ErrorCode}; retry {AutomaticRetryCount} of {AutomaticRetryLimit}; next retry {NextRetryAt}; elapsed {ElapsedMilliseconds} ms",
                    jobId,
                    currentStage,
                    "RetryScheduled",
                    attemptNumber,
                    currentProgress,
                    classification.ErrorCode,
                    nextRetryCount,
                    _retryOptions.RetryLimit,
                    nextRetryAt,
                    GetElapsedMilliseconds(stageStarted));

                throw CreateSafeException(classification);
            }

            await PersistFailureStateAsync(
                () => _meetingJobRepository.RecordFinalFailureAsync(
                    jobId,
                    currentStage,
                    currentProgress,
                    classification.ErrorCode,
                    classification.SafeMessage,
                    meetingJob.AutomaticRetryCount,
                    _retryOptions.RetryLimit,
                    CancellationToken.None),
                jobId,
                currentStage,
                stageStarted);

            throw CreateSafeException(classification);
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
        int attemptNumber,
        Action<MeetingJobStage> setStage,
        Action<int> setProgress)
    {
        if (!string.IsNullOrWhiteSpace(meetingJob.ProcessedFilePath) &&
            await _fileStorageService.ExistsAsync(
                meetingJob.ProcessedFilePath,
                CancellationToken.None))
        {
            LogStageOutcome(
                LogLevel.Information,
                jobId,
                MeetingJobStage.Transcoding,
                "CheckpointReused",
                25,
                attemptNumber,
                _timeProvider.GetTimestamp());
            return meetingJob.ProcessedFilePath;
        }

        var stageStarted = _timeProvider.GetTimestamp();
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

        LogStageOutcome(
            LogLevel.Information,
            jobId,
            MeetingJobStage.Transcoding,
            "Succeeded",
            25,
            attemptNumber,
            stageStarted);

        return processedFilePath;
    }

    private void LogStageOutcome(
        LogLevel logLevel,
        Guid jobId,
        MeetingJobStage stage,
        string outcome,
        int progress,
        int attempt,
        long startedTimestamp)
    {
        _logger.Log(
            logLevel,
            "Meeting processing event for job {JobId}; stage {Stage}; outcome {Outcome}; attempt {Attempt}; progress {Progress}; elapsed {ElapsedMilliseconds} ms",
            jobId,
            stage,
            outcome,
            attempt,
            progress,
            GetElapsedMilliseconds(startedTimestamp));
    }

    private long GetElapsedMilliseconds(long startedTimestamp)
    {
        return (long)_timeProvider.GetElapsedTime(startedTimestamp).TotalMilliseconds;
    }

    private async Task PersistFailureStateAsync(
        Func<Task> persist,
        Guid jobId,
        MeetingJobStage stage,
        long stageStarted)
    {
        try
        {
            await persist();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Meeting processing event for job {JobId}; stage {Stage}; outcome {Outcome}; error code {ErrorCode}; exception {ExceptionType}; elapsed {ElapsedMilliseconds} ms",
                jobId,
                stage,
                "FailureStatePersistenceFailed",
                MeetingErrorCodes.DatabaseUnavailable,
                exception.GetType().Name,
                GetElapsedMilliseconds(stageStarted));
            throw new MeetingProcessingAttemptException(
                MeetingErrorCodes.DatabaseUnavailable,
                "Meeting data storage is temporarily unavailable.");
        }
    }

    private static Exception CreateSafeException(MeetingFailureClassification classification)
    {
        return classification.Kind == MeetingFailureKind.Permanent
            ? new PermanentMeetingProcessingException(
                $"{classification.ErrorCode}: {classification.SafeMessage}")
            : new MeetingProcessingAttemptException(
                classification.ErrorCode,
                classification.SafeMessage);
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

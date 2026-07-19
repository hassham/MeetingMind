using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using MeetingMind.Infrastructure.Failures;
using MeetingMind.Worker.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeetingMind.Worker.Tests;

public sealed class MeetingProcessingJobTests
{
    [Fact]
    public async Task ProcessMeetingAsyncCompletesPipelineThroughAllAbstractions()
    {
        var harness = new ProcessingHarness();

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        Assert.Equal(1, harness.Audio.CallCount);
        Assert.Equal(1, harness.Transcription.CallCount);
        Assert.Equal(1, harness.Minutes.CallCount);
        Assert.Equal("Audio/Processed/meeting.wav", harness.Repository.Job.ProcessedFilePath);
        Assert.Equal("Test transcript", harness.Repository.Transcript?.TranscriptText);
        Assert.NotNull(harness.Repository.SavedMinutes);
        Assert.Equal(MeetingJobStatus.Completed, harness.Repository.Job.Status);
        Assert.Equal(MeetingJobStage.Completed, harness.Repository.Job.Stage);
        Assert.Equal(100, harness.Repository.Job.Progress);
    }

    [Fact]
    public async Task TransientFailureSchedulesRetryThenSucceedsFromTranscriptCheckpoint()
    {
        var harness = new ProcessingHarness();
        harness.Minutes.Exception = new InvalidOperationException("unknown provider interruption");

        var exception = await Assert.ThrowsAsync<MeetingProcessingAttemptException>(
            () => harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id));

        Assert.DoesNotContain("unknown provider interruption", exception.Message);
        Assert.Equal("unexpected_failure", exception.ErrorCode);
        Assert.Equal(MeetingJobStatus.Queued, harness.Repository.Job.Status);
        Assert.Equal(MeetingJobStage.GeneratingMinutes, harness.Repository.Job.Stage);
        Assert.Equal(60, harness.Repository.Job.Progress);
        Assert.Equal(1, harness.Repository.Job.AutomaticRetryCount);
        Assert.Equal(2, harness.Repository.Job.AutomaticRetryLimit);
        Assert.Equal(harness.Now.AddSeconds(10), harness.Repository.Job.NextRetryAt);
        Assert.Equal("Meeting processing failed temporarily and will be retried.", harness.Repository.Job.ErrorMessage);
        Assert.Equal("unexpected_failure", harness.Repository.Job.ErrorCode);

        harness.Minutes.Exception = null;
        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        Assert.Equal(1, harness.Audio.CallCount);
        Assert.Equal(1, harness.Transcription.CallCount);
        Assert.Equal(2, harness.Minutes.CallCount);
        Assert.Equal(MeetingJobStatus.Completed, harness.Repository.Job.Status);
        Assert.Equal(1, harness.Repository.Job.AutomaticRetryCount);
        Assert.Null(harness.Repository.Job.NextRetryAt);
    }

    [Fact]
    public async Task PermanentFailureIsRecordedOnceAndExcludedFromAutomaticRetry()
    {
        var harness = new ProcessingHarness();
        harness.Audio.Exception = new PermanentMeetingProcessingException("corrupt audio");

        await Assert.ThrowsAsync<PermanentMeetingProcessingException>(
            () => harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id));

        Assert.Equal(1, harness.Audio.CallCount);
        Assert.Equal(MeetingJobStatus.Failed, harness.Repository.Job.Status);
        Assert.Equal(MeetingJobStage.Transcoding, harness.Repository.Job.Stage);
        Assert.Equal(0, harness.Repository.Job.AutomaticRetryCount);
        Assert.Equal(2, harness.Repository.Job.AutomaticRetryLimit);
        Assert.Null(harness.Repository.Job.NextRetryAt);
        Assert.NotNull(harness.Repository.Job.CompletedAt);
    }

    [Fact]
    public async Task ExhaustedTransientFailureRemainsStableFailed()
    {
        var harness = new ProcessingHarness();
        harness.Repository.Job.AutomaticRetryCount = 2;
        harness.Repository.Job.AutomaticRetryLimit = 2;
        harness.Repository.Job.Status = MeetingJobStatus.Queued;
        harness.Audio.Exception = new InvalidOperationException("still unavailable");

        await Assert.ThrowsAsync<MeetingProcessingAttemptException>(
            () => harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id));

        Assert.Equal(MeetingJobStatus.Failed, harness.Repository.Job.Status);
        Assert.Equal(MeetingJobStage.Transcoding, harness.Repository.Job.Stage);
        Assert.Equal(2, harness.Repository.Job.AutomaticRetryCount);
        Assert.Null(harness.Repository.Job.NextRetryAt);
        Assert.NotNull(harness.Repository.Job.CompletedAt);
    }

    [Fact]
    public async Task LaterManualRetryReceivesFreshBudgetAndCanSucceed()
    {
        var harness = new ProcessingHarness();
        harness.Audio.Exception = new PermanentMeetingProcessingException("missing dependency");
        await Assert.ThrowsAsync<PermanentMeetingProcessingException>(
            () => harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id));

        await harness.Repository.ResetForRetryAsync(harness.Repository.Job.Id, CancellationToken.None);
        harness.Audio.Exception = null;

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        Assert.Equal(MeetingJobStatus.Completed, harness.Repository.Job.Status);
        Assert.Equal(0, harness.Repository.Job.AutomaticRetryCount);
        Assert.Equal(2, harness.Repository.Job.AutomaticRetryLimit);
    }

    [Fact]
    public async Task ValidProcessedAudioCheckpointSkipsConversion()
    {
        var harness = new ProcessingHarness();
        harness.Repository.Job.ProcessedFilePath = "Audio/Processed/existing.wav";
        harness.Storage.Files.Add("Audio/Processed/existing.wav");

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        Assert.Equal(0, harness.Audio.CallCount);
        Assert.Equal(1, harness.Transcription.CallCount);
        Assert.Equal(MeetingJobStatus.Completed, harness.Repository.Job.Status);
    }

    [Fact]
    public async Task MissingCheckpointArtifactFallsBackToEarlierStage()
    {
        var harness = new ProcessingHarness();
        harness.Repository.Job.ProcessedFilePath = "Audio/Processed/missing.wav";
        harness.Repository.Transcript = new MeetingTranscript
        {
            MeetingJobId = harness.Repository.Job.Id,
            TranscriptText = "Old transcript",
            TranscriptFilePath = "Transcript/missing.txt"
        };

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        Assert.Equal(1, harness.Audio.CallCount);
        Assert.Equal(1, harness.Transcription.CallCount);
        Assert.Equal("Test transcript", harness.Repository.Transcript.TranscriptText);
    }

    [Fact]
    public async Task LongMeetingProgressIsPersistedWithinGeneratingMinutesStage()
    {
        var harness = new ProcessingHarness();
        harness.Minutes.ProgressUpdates = [68, 85, 87, 89];

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        Assert.Equal(
            [60, 68, 85, 87, 89],
            harness.Repository.StatusUpdates
                .Where(update => update.Stage == MeetingJobStage.GeneratingMinutes)
                .Select(update => update.Progress));
        Assert.Equal(MeetingJobStatus.Completed, harness.Repository.Job.Status);
    }

    [Fact]
    public async Task PartialCallFailureSchedulesRetryAtLatestGenerationProgress()
    {
        var harness = new ProcessingHarness();
        harness.Minutes.ProgressUpdates = [70];
        harness.Minutes.Exception = new InvalidOperationException("partial provider interruption");

        await Assert.ThrowsAsync<MeetingProcessingAttemptException>(() =>
            harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id));

        Assert.Equal(MeetingJobStatus.Queued, harness.Repository.Job.Status);
        Assert.Equal(MeetingJobStage.GeneratingMinutes, harness.Repository.Job.Stage);
        Assert.Equal(70, harness.Repository.Job.Progress);
        Assert.Equal(1, harness.Repository.Job.AutomaticRetryCount);
    }

    [Fact]
    public async Task SuccessfulRunLogsStructuredStageOutcomesAndElapsedTime()
    {
        var harness = new ProcessingHarness();

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        var logs = string.Join(Environment.NewLine, harness.Logger.Messages);
        Assert.Contains("stage Validating; outcome Succeeded", logs);
        Assert.Contains("stage Transcoding; outcome Succeeded", logs);
        Assert.Contains("stage Transcribing; outcome Succeeded", logs);
        Assert.Contains("stage GeneratingMinutes; outcome Succeeded", logs);
        Assert.Contains("stage SavingResults; outcome Succeeded", logs);
        Assert.Contains("stage Completed; outcome Succeeded", logs);
        Assert.Contains("elapsed", logs);
    }

    [Fact]
    public async Task FailureLogsAndThrownExceptionExcludeRawTechnicalDetail()
    {
        var harness = new ProcessingHarness();
        const string sensitiveDetail = "C:\\private\\meeting.wav provider payload transcript fragment";
        harness.Minutes.Exception = new InvalidOperationException(sensitiveDetail);

        var exception = await Assert.ThrowsAsync<MeetingProcessingAttemptException>(() =>
            harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id));
        var logs = string.Join(Environment.NewLine, harness.Logger.Messages);

        Assert.DoesNotContain(sensitiveDetail, logs);
        Assert.DoesNotContain(sensitiveDetail, exception.Message);
        Assert.Contains("error code unexpected_failure", logs);
        Assert.Contains("exception InvalidOperationException", logs);
        Assert.All(harness.Logger.Exceptions, loggedException => Assert.Null(loggedException));
    }

    private sealed class ProcessingHarness
    {
        public ProcessingHarness()
        {
            TimeProvider = new FixedTimeProvider(Now);
            Job = new MeetingProcessingJob(
                Logger,
                Audio,
                Storage,
                Repository,
                Transcription,
                Minutes,
                new MeetingFailureClassifier(),
                RetryOptions,
                TimeProvider);
        }

        public DateTimeOffset Now { get; } = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

        public StubAudioProcessingService Audio { get; } = new();

        public StubFileStorageService Storage { get; } = new();

        public StubMeetingJobRepository Repository { get; } = new();

        public StubTranscriptionService Transcription { get; } = new();

        public StubMinutesService Minutes { get; } = new();

        public CapturingLogger<MeetingProcessingJob> Logger { get; } = new();

        public AutomaticRetryOptions RetryOptions { get; } = new() { DelaysInSeconds = [10, 60] };

        public FixedTimeProvider TimeProvider { get; }

        public MeetingProcessingJob Job { get; }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubAudioProcessingService : IAudioProcessingService
    {
        public Exception? Exception { get; set; }

        public int CallCount { get; private set; }

        public Task<string> ConvertToStandardFormatAsync(string inputPath, CancellationToken cancellationToken)
        {
            CallCount++;
            return Exception is null
                ? Task.FromResult("Audio/Processed/meeting.wav")
                : Task.FromException<string>(Exception);
        }
    }

    private sealed class StubTranscriptionService : ITranscriptionService
    {
        public int CallCount { get; private set; }

        public Task<string> TranscribeAsync(string audioPath, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult("Test transcript");
        }
    }

    private sealed class StubMinutesService : IMeetingMinutesService
    {
        public Exception? Exception { get; set; }

        public int[] ProgressUpdates { get; set; } = [];

        public int CallCount { get; private set; }

        public async Task<MeetingMinutesContent> GenerateMinutesAsync(
            string transcriptText,
            CancellationToken cancellationToken,
            Func<MeetingMinutesGenerationProgress, CancellationToken, Task>? progressCallback = null)
        {
            CallCount++;
            foreach (var percent in ProgressUpdates)
            {
                if (progressCallback is not null)
                {
                    await progressCallback(
                        new MeetingMinutesGenerationProgress(percent, "test", percent, 100),
                        cancellationToken);
                }
            }

            if (Exception is not null)
            {
                throw Exception;
            }

            return new MeetingMinutesContent(
                "Test meeting",
                "Test summary",
                ["Hasham"],
                ["Testing"],
                ["Add coverage"],
                [new MeetingActionItem("Write tests", "Hasham", null)],
                ["Docker unavailable"],
                ["Run tests"]);
        }
    }

    private sealed class StubFileStorageService : IFileStorageService
    {
        public HashSet<string> Files { get; } = [];

        public Task<string> SaveOriginalAudioAsync(Stream file, string originalFileName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> SaveTranscriptAsync(Guid meetingJobId, string transcriptText, CancellationToken cancellationToken)
        {
            var path = $"Transcript/{meetingJobId:N}.txt";
            Files.Add(path);
            return Task.FromResult(path);
        }

        public Task<string> SaveMinutesAsync(Guid meetingJobId, string minutesMarkdown, CancellationToken cancellationToken)
        {
            var path = $"Minutes/{meetingJobId:N}.md";
            Files.Add(path);
            return Task.FromResult(path);
        }

        public Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Files.Contains(filePath));
        }

        public Task<Stream> ReadAsync(string filePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void EnsurePathIsSafe(string filePath)
        {
        }

        public Task DeleteAsync(string filePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubMeetingJobRepository : IMeetingJobRepository
    {
        public MeetingJob Job { get; } = new()
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "meeting.mp3",
            OriginalFilePath = "Audio/Original/meeting.mp3",
            Status = MeetingJobStatus.Queued,
            Stage = MeetingJobStage.Uploaded,
            CreatedAt = new DateTimeOffset(2026, 7, 18, 11, 0, 0, TimeSpan.Zero)
        };

        public MeetingTranscript? Transcript { get; set; }

        public MeetingMinutes? SavedMinutes { get; private set; }

        public List<StatusUpdate> StatusUpdates { get; } = [];

        public Task AddAsync(MeetingJob meetingJob, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MeetingJob?> GetByIdAsync(Guid meetingJobId, CancellationToken cancellationToken) =>
            Task.FromResult<MeetingJob?>(meetingJobId == Job.Id ? Job : null);

        public Task<MeetingTranscript?> GetTranscriptByJobIdAsync(Guid meetingJobId, CancellationToken cancellationToken) =>
            Task.FromResult(meetingJobId == Job.Id ? Transcript : null);

        public Task<MeetingMinutes?> GetMinutesByJobIdAsync(Guid meetingJobId, CancellationToken cancellationToken) =>
            Task.FromResult<MeetingMinutes?>(SavedMinutes);

        public Task<IReadOnlyList<MeetingJob>> GetHistoryAsync(int skip, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetHangfireJobIdAsync(Guid meetingJobId, string hangfireJobId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetProcessedFilePathAsync(Guid meetingJobId, string processedFilePath, CancellationToken cancellationToken)
        {
            Job.ProcessedFilePath = processedFilePath;
            return Task.CompletedTask;
        }

        public Task SaveTranscriptAsync(
            Guid meetingJobId,
            string transcriptText,
            string transcriptFilePath,
            CancellationToken cancellationToken)
        {
            Transcript = new MeetingTranscript
            {
                MeetingJobId = meetingJobId,
                TranscriptText = transcriptText,
                TranscriptFilePath = transcriptFilePath
            };
            return Task.CompletedTask;
        }

        public Task SaveMinutesAsync(Guid meetingJobId, MeetingMinutes minutes, CancellationToken cancellationToken)
        {
            SavedMinutes = minutes;
            return Task.CompletedTask;
        }

        public Task ResetForRetryAsync(Guid meetingJobId, CancellationToken cancellationToken)
        {
            Job.Status = MeetingJobStatus.Queued;
            Job.Stage = MeetingJobStage.Uploaded;
            Job.Progress = 0;
            Job.ErrorMessage = null;
            Job.AutomaticRetryCount = 0;
            Job.AutomaticRetryLimit = 0;
            Job.NextRetryAt = null;
            return Task.CompletedTask;
        }

        public Task BeginProcessingAsync(Guid meetingJobId, int automaticRetryLimit, CancellationToken cancellationToken)
        {
            Job.Status = MeetingJobStatus.Processing;
            Job.Stage = MeetingJobStage.Validating;
            Job.Progress = 0;
            Job.ErrorCode = null;
            Job.ErrorMessage = null;
            Job.AutomaticRetryLimit = automaticRetryLimit;
            Job.NextRetryAt = null;
            return Task.CompletedTask;
        }

        public Task ScheduleAutomaticRetryAsync(
            Guid meetingJobId,
            MeetingJobStage stage,
            int progress,
            string errorCode,
            string errorMessage,
            int automaticRetryCount,
            int automaticRetryLimit,
            DateTimeOffset nextRetryAt,
            CancellationToken cancellationToken)
        {
            Job.Status = MeetingJobStatus.Queued;
            Job.Stage = stage;
            Job.Progress = progress;
            Job.ErrorCode = errorCode;
            Job.ErrorMessage = errorMessage;
            Job.AutomaticRetryCount = automaticRetryCount;
            Job.AutomaticRetryLimit = automaticRetryLimit;
            Job.NextRetryAt = nextRetryAt;
            return Task.CompletedTask;
        }

        public Task RecordFinalFailureAsync(
            Guid meetingJobId,
            MeetingJobStage stage,
            int progress,
            string errorCode,
            string errorMessage,
            int automaticRetryCount,
            int automaticRetryLimit,
            CancellationToken cancellationToken)
        {
            Job.Status = MeetingJobStatus.Failed;
            Job.Stage = stage;
            Job.Progress = progress;
            Job.ErrorCode = errorCode;
            Job.ErrorMessage = errorMessage;
            Job.AutomaticRetryCount = automaticRetryCount;
            Job.AutomaticRetryLimit = automaticRetryLimit;
            Job.NextRetryAt = null;
            Job.CompletedAt = DateTimeOffset.UtcNow;
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
            StatusUpdates.Add(new StatusUpdate(stage, progress));
            Job.Status = status;
            Job.Stage = stage;
            Job.Progress = progress;
            Job.ErrorCode = errorMessage is null ? null : Job.ErrorCode;
            Job.ErrorMessage = errorMessage;
            Job.NextRetryAt = null;
            return Task.CompletedTask;
        }

        public sealed record StatusUpdate(MeetingJobStage Stage, int Progress);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public List<Exception?> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }
}

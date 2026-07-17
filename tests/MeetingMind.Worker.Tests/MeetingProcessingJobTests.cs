using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using MeetingMind.Worker.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeetingMind.Worker.Tests;

public sealed class MeetingProcessingJobTests
{
    [Fact]
    public async Task ProcessMeetingAsyncCompletesPipelineThroughAllAbstractions()
    {
        var harness = new ProcessingHarness();

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        Assert.Equal(
            [
                MeetingJobStage.Validating,
                MeetingJobStage.Transcoding,
                MeetingJobStage.Transcribing,
                MeetingJobStage.GeneratingMinutes,
                MeetingJobStage.SavingResults,
                MeetingJobStage.Completed
            ],
            harness.Repository.StatusUpdates.Select(update => update.Stage));
        Assert.Equal("Audio/Processed/meeting.wav", harness.Repository.ProcessedFilePath);
        Assert.Equal("Test transcript", harness.Repository.SavedTranscriptText);
        Assert.NotNull(harness.Repository.SavedMinutes);
        Assert.Equal("Test meeting", harness.Repository.SavedMinutes.Title);
        Assert.Equal(MeetingJobStatus.Completed, harness.Repository.StatusUpdates[^1].Status);
        Assert.Equal(100, harness.Repository.StatusUpdates[^1].Progress);
    }

    [Fact]
    public async Task ConversionFailureRecordsTranscodingStage()
    {
        var harness = new ProcessingHarness();
        harness.Audio.Exception = new InvalidOperationException("conversion failed");

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        AssertFailure(harness, MeetingJobStage.Transcoding, 10, "conversion failed");
        Assert.Null(harness.Repository.SavedTranscriptText);
    }

    [Fact]
    public async Task TranscriptionFailureRecordsTranscribingStage()
    {
        var harness = new ProcessingHarness();
        harness.Transcription.Exception = new InvalidOperationException("transcription failed");

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        AssertFailure(harness, MeetingJobStage.Transcribing, 25, "transcription failed");
        Assert.Null(harness.Repository.SavedTranscriptText);
    }

    [Fact]
    public async Task TranscriptStorageFailureRecordsTranscribingStage()
    {
        var harness = new ProcessingHarness();
        harness.Storage.TranscriptException = new IOException("storage failed");

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        AssertFailure(harness, MeetingJobStage.Transcribing, 25, "storage failed");
        Assert.Null(harness.Repository.SavedTranscriptText);
    }

    [Fact]
    public async Task MinutesProviderFailureRecordsGeneratingMinutesStage()
    {
        var harness = new ProcessingHarness();
        harness.Minutes.Exception = new InvalidOperationException("minutes failed");

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        AssertFailure(harness, MeetingJobStage.GeneratingMinutes, 60, "minutes failed");
        Assert.Null(harness.Repository.SavedMinutes);
    }

    [Fact]
    public async Task PersistenceFailureRecordsSavingResultsStage()
    {
        var harness = new ProcessingHarness();
        harness.Repository.SaveMinutesException = new InvalidOperationException("persistence failed");

        await harness.Job.ProcessMeetingAsync(harness.Repository.Job.Id);

        AssertFailure(harness, MeetingJobStage.SavingResults, 90, "persistence failed");
    }

    private static void AssertFailure(
        ProcessingHarness harness,
        MeetingJobStage stage,
        int progress,
        string error)
    {
        var failure = harness.Repository.StatusUpdates[^1];
        Assert.Equal(MeetingJobStatus.Failed, failure.Status);
        Assert.Equal(stage, failure.Stage);
        Assert.Equal(progress, failure.Progress);
        Assert.Equal(error, failure.ErrorMessage);
    }

    private sealed class ProcessingHarness
    {
        public ProcessingHarness()
        {
            Job = new MeetingProcessingJob(
                NullLogger<MeetingProcessingJob>.Instance,
                Audio,
                Storage,
                Repository,
                Transcription,
                Minutes);
        }

        public StubAudioProcessingService Audio { get; } = new();

        public StubFileStorageService Storage { get; } = new();

        public StubMeetingJobRepository Repository { get; } = new();

        public StubTranscriptionService Transcription { get; } = new();

        public StubMinutesService Minutes { get; } = new();

        public MeetingProcessingJob Job { get; }
    }

    private sealed class StubAudioProcessingService : IAudioProcessingService
    {
        public Exception? Exception { get; set; }

        public Task<string> ConvertToStandardFormatAsync(string inputPath, CancellationToken cancellationToken)
        {
            return Exception is null
                ? Task.FromResult("Audio/Processed/meeting.wav")
                : Task.FromException<string>(Exception);
        }
    }

    private sealed class StubTranscriptionService : ITranscriptionService
    {
        public Exception? Exception { get; set; }

        public Task<string> TranscribeAsync(string audioPath, CancellationToken cancellationToken)
        {
            return Exception is null
                ? Task.FromResult("Test transcript")
                : Task.FromException<string>(Exception);
        }
    }

    private sealed class StubMinutesService : IMeetingMinutesService
    {
        public Exception? Exception { get; set; }

        public Task<MeetingMinutesContent> GenerateMinutesAsync(
            string transcriptText,
            CancellationToken cancellationToken)
        {
            if (Exception is not null)
            {
                return Task.FromException<MeetingMinutesContent>(Exception);
            }

            return Task.FromResult(new MeetingMinutesContent(
                "Test meeting",
                "Test summary",
                ["Hasham"],
                ["Testing"],
                ["Add coverage"],
                [new MeetingActionItem("Write tests", "Hasham", null)],
                ["Docker unavailable"],
                ["Run tests"]));
        }
    }

    private sealed class StubFileStorageService : IFileStorageService
    {
        public Exception? TranscriptException { get; set; }

        public Task<string> SaveOriginalAudioAsync(
            Stream file,
            string originalFileName,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> SaveTranscriptAsync(
            Guid meetingJobId,
            string transcriptText,
            CancellationToken cancellationToken)
        {
            return TranscriptException is null
                ? Task.FromResult($"Transcript/{meetingJobId:N}.txt")
                : Task.FromException<string>(TranscriptException);
        }

        public Task<string> SaveMinutesAsync(
            Guid meetingJobId,
            string minutesMarkdown,
            CancellationToken cancellationToken)
        {
            return Task.FromResult($"Minutes/{meetingJobId:N}.md");
        }

        public Task<Stream> ReadAsync(string filePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(string filePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubMeetingJobRepository : IMeetingJobRepository
    {
        public MeetingJob Job { get; } = new()
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "meeting.mp3",
            OriginalFilePath = "Audio/Original/meeting.mp3"
        };

        public List<StatusUpdate> StatusUpdates { get; } = [];

        public string? ProcessedFilePath { get; private set; }

        public string? SavedTranscriptText { get; private set; }

        public MeetingMinutes? SavedMinutes { get; private set; }

        public Exception? SaveMinutesException { get; set; }

        public Task AddAsync(MeetingJob meetingJob, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<MeetingJob?> GetByIdAsync(Guid meetingJobId, CancellationToken cancellationToken)
        {
            return Task.FromResult<MeetingJob?>(meetingJobId == Job.Id ? Job : null);
        }

        public Task<MeetingTranscript?> GetTranscriptByJobIdAsync(
            Guid meetingJobId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<MeetingMinutes?> GetMinutesByJobIdAsync(
            Guid meetingJobId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MeetingJob>> GetHistoryAsync(
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetHangfireJobIdAsync(
            Guid meetingJobId,
            string hangfireJobId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetProcessedFilePathAsync(
            Guid meetingJobId,
            string processedFilePath,
            CancellationToken cancellationToken)
        {
            ProcessedFilePath = processedFilePath;
            return Task.CompletedTask;
        }

        public Task SaveTranscriptAsync(
            Guid meetingJobId,
            string transcriptText,
            string transcriptFilePath,
            CancellationToken cancellationToken)
        {
            SavedTranscriptText = transcriptText;
            return Task.CompletedTask;
        }

        public Task SaveMinutesAsync(
            Guid meetingJobId,
            MeetingMinutes minutes,
            CancellationToken cancellationToken)
        {
            if (SaveMinutesException is not null)
            {
                return Task.FromException(SaveMinutesException);
            }

            SavedMinutes = minutes;
            return Task.CompletedTask;
        }

        public Task ResetForRetryAsync(Guid meetingJobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task UpdateStatusAsync(
            Guid meetingJobId,
            MeetingJobStatus status,
            MeetingJobStage stage,
            int progress,
            string? errorMessage,
            CancellationToken cancellationToken)
        {
            StatusUpdates.Add(new StatusUpdate(status, stage, progress, errorMessage));
            return Task.CompletedTask;
        }
    }

    private sealed record StatusUpdate(
        MeetingJobStatus Status,
        MeetingJobStage Stage,
        int Progress,
        string? ErrorMessage);
}

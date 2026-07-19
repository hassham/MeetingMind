using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Common.Retention;
using MeetingMind.Application.Operations;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Unit.Tests;

public sealed class StorageRetentionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DisabledPolicyDoesNotInspectOrDeleteJobs()
    {
        var repository = new StubRetentionRepository();
        var storage = new StubStorage();
        var service = CreateService(repository, storage, enabled: false);

        var result = await service.CleanupAsync(CancellationToken.None);

        Assert.Equal(0, result.Candidates);
        Assert.Equal(0, repository.QueryCount);
        Assert.Empty(storage.DeletedPaths);
    }

    [Fact]
    public async Task EligibleTerminalJobDeletesEveryArtifactBeforeDatabaseRecord()
    {
        var candidate = CreateCandidate();
        var repository = new StubRetentionRepository(candidate);
        var storage = new StubStorage();
        var service = CreateService(repository, storage);

        var result = await service.CleanupAsync(CancellationToken.None);

        Assert.Equal(1, result.Deleted);
        Assert.Equal(
            [candidate.OriginalFilePath, candidate.ProcessedFilePath, candidate.TranscriptFilePath, candidate.MinutesFilePath],
            storage.DeletedPaths);
        Assert.True(repository.DatabaseRecordDeleted);
    }

    [Fact]
    public async Task JobThatBecomesActiveDuringRevalidationIsNeverTouched()
    {
        var repository = new StubRetentionRepository(CreateCandidate())
        {
            ReturnCandidateOnRevalidation = false
        };
        var storage = new StubStorage();
        var service = CreateService(repository, storage);

        var result = await service.CleanupAsync(CancellationToken.None);

        Assert.Equal(1, result.Skipped);
        Assert.Empty(storage.DeletedPaths);
        Assert.False(repository.DatabaseRecordDeleted);
    }

    [Fact]
    public async Task UnsafePathPreventsAllArtifactDeletionAndKeepsDatabaseRecord()
    {
        var candidate = CreateCandidate() with { TranscriptFilePath = "../outside.txt" };
        var repository = new StubRetentionRepository(candidate);
        var storage = new StubStorage { UnsafePath = "../outside.txt" };
        var service = CreateService(repository, storage);

        var result = await service.CleanupAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Empty(storage.DeletedPaths);
        Assert.False(repository.DatabaseRecordDeleted);
        Assert.Equal("transcript", Assert.Single(result.Failures).ArtifactType);
    }

    [Fact]
    public async Task PartialFileFailureKeepsDatabaseRecordForLaterRetry()
    {
        var candidate = CreateCandidate();
        var repository = new StubRetentionRepository(candidate);
        var storage = new StubStorage { FailingPath = candidate.TranscriptFilePath };
        var service = CreateService(repository, storage);

        var result = await service.CleanupAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.False(repository.DatabaseRecordDeleted);
        Assert.Equal("transcript", Assert.Single(result.Failures).ArtifactType);
    }

    private static StorageRetentionService CreateService(
        StubRetentionRepository repository,
        StubStorage storage,
        bool enabled = true)
    {
        return new StorageRetentionService(
            repository,
            storage,
            new StorageRetentionOptions
            {
                Enabled = enabled,
                RetentionDays = 30,
                BatchSize = 100
            },
            new FixedTimeProvider(Now));
    }

    private static StorageRetentionCandidate CreateCandidate()
    {
        return new StorageRetentionCandidate(
            Guid.NewGuid(),
            MeetingJobStatus.Completed,
            Now.AddDays(-31),
            "Audio/Original/original.mp3",
            "Audio/Processed/processed.wav",
            "Transcript/transcript.txt",
            "Minutes/minutes.md");
    }

    private sealed class StubRetentionRepository : IStorageRetentionRepository
    {
        private readonly StorageRetentionCandidate? _candidate;

        public StubRetentionRepository(StorageRetentionCandidate? candidate = null)
        {
            _candidate = candidate;
        }

        public int QueryCount { get; private set; }
        public bool ReturnCandidateOnRevalidation { get; set; } = true;
        public bool DatabaseRecordDeleted { get; private set; }

        public Task<IReadOnlyList<Guid>> GetExpiredTerminalJobIdsAsync(
            DateTimeOffset cutoff,
            int take,
            CancellationToken cancellationToken)
        {
            QueryCount++;
            IReadOnlyList<Guid> ids = _candidate is null ? [] : [_candidate.JobId];
            return Task.FromResult(ids);
        }

        public async Task<bool> DeleteEligibleJobWithArtifactsAsync(
            Guid jobId,
            DateTimeOffset cutoff,
            Func<StorageRetentionCandidate, CancellationToken, Task> deleteArtifacts,
            CancellationToken cancellationToken)
        {
            if (!ReturnCandidateOnRevalidation || _candidate is null)
            {
                return false;
            }

            await deleteArtifacts(_candidate, cancellationToken);
            DatabaseRecordDeleted = true;
            return true;
        }
    }

    private sealed class StubStorage : IFileStorageService
    {
        public List<string> DeletedPaths { get; } = [];
        public string? UnsafePath { get; init; }
        public string? FailingPath { get; init; }

        public void EnsurePathIsSafe(string filePath)
        {
            if (filePath == UnsafePath)
            {
                throw new PermanentMeetingProcessingException("Unsafe test path.");
            }
        }

        public Task DeleteAsync(string filePath, CancellationToken cancellationToken)
        {
            if (filePath == FailingPath)
            {
                throw new IOException("Test deletion failure.");
            }

            DeletedPaths.Add(filePath);
            return Task.CompletedTask;
        }

        public Task<string> SaveOriginalAudioAsync(Stream file, string originalFileName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> SaveTranscriptAsync(Guid meetingJobId, string transcriptText, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> SaveMinutesAsync(Guid meetingJobId, string minutesMarkdown, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream> ReadAsync(string filePath, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

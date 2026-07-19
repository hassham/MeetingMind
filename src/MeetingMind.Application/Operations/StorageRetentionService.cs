using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Common.Retention;

namespace MeetingMind.Application.Operations;

public sealed class StorageRetentionService : IStorageRetentionService
{
    private readonly IStorageRetentionRepository _repository;
    private readonly IFileStorageService _fileStorageService;
    private readonly StorageRetentionOptions _options;
    private readonly TimeProvider _timeProvider;

    public StorageRetentionService(
        IStorageRetentionRepository repository,
        IFileStorageService fileStorageService,
        StorageRetentionOptions options,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _fileStorageService = fileStorageService;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<StorageRetentionResult> CleanupAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new StorageRetentionResult(0, 0, 0, 0, []);
        }

        var cutoff = _timeProvider.GetUtcNow().AddDays(-_options.RetentionDays);
        var jobIds = await _repository.GetExpiredTerminalJobIdsAsync(
            cutoff,
            _options.BatchSize,
            cancellationToken);

        var deleted = 0;
        var skipped = 0;
        var failed = 0;
        var failures = new List<StorageRetentionFailure>();

        foreach (var jobId in jobIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentArtifactType = "path_validation";
            try
            {
                var wasDeleted = await _repository.DeleteEligibleJobWithArtifactsAsync(
                    jobId,
                    cutoff,
                    async (candidate, deleteCancellationToken) =>
                    {
                        var artifacts = GetArtifacts(candidate);
                        foreach (var artifact in artifacts)
                        {
                            currentArtifactType = artifact.Type;
                            _fileStorageService.EnsurePathIsSafe(artifact.Path);
                        }

                        foreach (var artifact in artifacts)
                        {
                            currentArtifactType = artifact.Type;
                            await _fileStorageService.DeleteAsync(
                                artifact.Path,
                                deleteCancellationToken);
                        }
                    },
                    cancellationToken);

                if (wasDeleted)
                {
                    deleted++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failed++;
                failures.Add(new StorageRetentionFailure(
                    jobId,
                    currentArtifactType,
                    exception.GetType().Name));
            }
        }

        return new StorageRetentionResult(jobIds.Count, deleted, skipped, failed, failures);
    }

    private static IReadOnlyList<(string Type, string Path)> GetArtifacts(StorageRetentionCandidate candidate)
    {
        var artifacts = new List<(string Type, string Path)>
        {
            ("original_audio", candidate.OriginalFilePath)
        };

        AddIfPresent(artifacts, "processed_audio", candidate.ProcessedFilePath);
        AddIfPresent(artifacts, "transcript", candidate.TranscriptFilePath);
        AddIfPresent(artifacts, "minutes", candidate.MinutesFilePath);
        return artifacts;
    }

    private static void AddIfPresent(
        ICollection<(string Type, string Path)> artifacts,
        string type,
        string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            artifacts.Add((type, path));
        }
    }
}

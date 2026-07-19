using MeetingMind.Application.Common.Interfaces;

namespace MeetingMind.Worker.Jobs;

public sealed class StorageRetentionJob : IStorageRetentionJob
{
    private readonly IStorageRetentionService _retentionService;
    private readonly ILogger<StorageRetentionJob> _logger;
    private readonly TimeProvider _timeProvider;

    public StorageRetentionJob(
        IStorageRetentionService retentionService,
        ILogger<StorageRetentionJob> logger,
        TimeProvider timeProvider)
    {
        _retentionService = retentionService;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task RunAsync()
    {
        var started = _timeProvider.GetTimestamp();
        var result = await _retentionService.CleanupAsync(CancellationToken.None);
        var elapsedMilliseconds = (long)_timeProvider.GetElapsedTime(started).TotalMilliseconds;

        foreach (var failure in result.Failures)
        {
            _logger.LogWarning(
                "Storage retention artifact deletion failed for job {JobId}; artifact {ArtifactType}; exception {ExceptionType}",
                failure.JobId,
                failure.ArtifactType,
                failure.ExceptionType);
        }

        _logger.LogInformation(
            "Storage retention completed with outcome {Outcome}; candidates {Candidates}; deleted {Deleted}; skipped {Skipped}; failed {Failed}; elapsed {ElapsedMilliseconds} ms",
            result.Failed == 0 ? "Succeeded" : "PartialFailure",
            result.Candidates,
            result.Deleted,
            result.Skipped,
            result.Failed,
            elapsedMilliseconds);
    }
}

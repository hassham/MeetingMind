using MeetingMind.Application.Common.Retention;

namespace MeetingMind.Application.Common.Interfaces;

public interface IStorageRetentionRepository
{
    Task<IReadOnlyList<Guid>> GetExpiredTerminalJobIdsAsync(
        DateTimeOffset cutoff,
        int take,
        CancellationToken cancellationToken);

    Task<bool> DeleteEligibleJobWithArtifactsAsync(
        Guid jobId,
        DateTimeOffset cutoff,
        Func<StorageRetentionCandidate, CancellationToken, Task> deleteArtifacts,
        CancellationToken cancellationToken);
}

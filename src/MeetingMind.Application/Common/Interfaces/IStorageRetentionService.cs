using MeetingMind.Application.Common.Retention;

namespace MeetingMind.Application.Common.Interfaces;

public interface IStorageRetentionService
{
    Task<StorageRetentionResult> CleanupAsync(CancellationToken cancellationToken);
}

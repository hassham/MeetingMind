namespace MeetingMind.Application.Common.Retention;

public sealed record StorageRetentionResult(
    int Candidates,
    int Deleted,
    int Skipped,
    int Failed,
    IReadOnlyList<StorageRetentionFailure> Failures);

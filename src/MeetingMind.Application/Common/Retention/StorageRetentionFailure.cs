namespace MeetingMind.Application.Common.Retention;

public sealed record StorageRetentionFailure(
    Guid JobId,
    string ArtifactType,
    string ExceptionType);

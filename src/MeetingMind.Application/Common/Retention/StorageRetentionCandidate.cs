using MeetingMind.Domain.Enums;

namespace MeetingMind.Application.Common.Retention;

public sealed record StorageRetentionCandidate(
    Guid JobId,
    MeetingJobStatus Status,
    MeetingProcessingMode ProcessingMode,
    DateTimeOffset RetentionTimestamp,
    string OriginalFilePath,
    string? ProcessedFilePath,
    string? TranscriptFilePath,
    string? MinutesFilePath);

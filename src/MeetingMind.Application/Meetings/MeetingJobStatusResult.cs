namespace MeetingMind.Application.Meetings;

public sealed record MeetingJobStatusResult(
    Guid JobId,
    string Status,
    string Stage,
    int Progress,
    string? ErrorMessage);

namespace MeetingMind.Application.Meetings;

public sealed record MeetingRetryResult(
    Guid JobId,
    string Status,
    string Stage,
    MeetingRetryFailureReason? FailureReason = null);

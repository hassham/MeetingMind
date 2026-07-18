namespace MeetingMind.Application.Meetings;

public sealed record MeetingDuration(
    long ProcessingDurationSeconds,
    long TotalDurationSeconds);

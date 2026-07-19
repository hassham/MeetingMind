namespace MeetingMind.Application.Meetings;

public sealed record MeetingMinutesGenerationProgress(
    int Percent,
    string Phase,
    int Completed,
    int Total);

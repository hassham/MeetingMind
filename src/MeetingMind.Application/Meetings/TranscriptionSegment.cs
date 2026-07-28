namespace MeetingMind.Application.Meetings;

public sealed record TranscriptionSegment(
    TimeSpan Start,
    TimeSpan End,
    string Text);

namespace MeetingMind.Application.Meetings;

public sealed record TranscriptParagraph(
    string Text,
    TimeSpan? Start,
    TimeSpan? End);

namespace MeetingMind.Application.Meetings;

public sealed record TranscriptionResult(
    IReadOnlyList<TranscriptionSegment> Segments);

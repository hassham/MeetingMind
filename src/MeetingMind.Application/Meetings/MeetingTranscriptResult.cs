namespace MeetingMind.Application.Meetings;

public sealed record MeetingTranscriptResult(
    Guid JobId,
    bool HasTimestamps,
    string? FormattingVersion,
    IReadOnlyList<MeetingTranscriptParagraphResult> Paragraphs);

public sealed record MeetingTranscriptParagraphResult(
    string Text,
    double? StartSeconds,
    double? EndSeconds);

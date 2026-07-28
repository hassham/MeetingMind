namespace MeetingMind.Application.Meetings;

public sealed record FormattedTranscript(
    IReadOnlyList<TranscriptParagraph> Paragraphs,
    string Text,
    TranscriptFormattingSnapshot Formatting);

namespace MeetingMind.Application.Meetings;

public sealed record StructuredTranscriptCheckpoint(
    IReadOnlyList<TranscriptionSegment> Segments,
    IReadOnlyList<TranscriptParagraph> Paragraphs,
    TranscriptFormattingSnapshot Formatting);

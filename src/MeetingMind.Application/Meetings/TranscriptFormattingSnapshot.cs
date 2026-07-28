namespace MeetingMind.Application.Meetings;

public sealed record TranscriptFormattingSnapshot(
    double SilenceGapSeconds,
    int PreferredParagraphCharacters,
    int HardParagraphCharacters,
    string FormattingVersion);

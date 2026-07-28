using MeetingMind.Application.Meetings;

namespace MeetingMind.Application.Common.Options;

public sealed class TranscriptFormattingOptions
{
    public const string SupportedVersion = "v1";

    public double SilenceGapSeconds { get; set; } = 1.5;

    public int PreferredParagraphCharacters { get; set; } = 300;

    public int HardParagraphCharacters { get; set; } = 700;

    public string FormattingVersion { get; set; } = SupportedVersion;

    public TranscriptFormattingSnapshot ToSnapshot() =>
        new(
            SilenceGapSeconds,
            PreferredParagraphCharacters,
            HardParagraphCharacters,
            FormattingVersion);
}

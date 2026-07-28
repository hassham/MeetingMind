using System.Text;
using System.Text.RegularExpressions;

namespace MeetingMind.Application.Meetings;

public sealed partial class TranscriptFormatter
{
    public FormattedTranscript Format(
        TranscriptionResult result,
        TranscriptFormattingSnapshot formatting)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateFormatting(formatting);

        var segments = NormalizeSegments(result.Segments);
        if (segments.Count == 0)
        {
            return new FormattedTranscript([], string.Empty, formatting);
        }

        var paragraphs = new List<TranscriptParagraph>();
        var current = new List<TranscriptionSegment>();
        var currentLength = 0;

        foreach (var segment in segments)
        {
            var silenceRequiresBreak =
                current.Count > 0 &&
                segment.Start - current[^1].End >=
                TimeSpan.FromSeconds(formatting.SilenceGapSeconds);

            if (silenceRequiresBreak)
            {
                AddParagraph(paragraphs, current);
                currentLength = 0;
            }

            var words = segment.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var separatorLength = currentLength == 0 ? 0 : 1;
                var wouldExceedHardLimit =
                    currentLength > 0 &&
                    currentLength + separatorLength + word.Length >
                    formatting.HardParagraphCharacters;

                if (wouldExceedHardLimit)
                {
                    AddParagraph(paragraphs, current);
                    currentLength = 0;
                }

                AppendWord(current, segment, word);
                currentLength += (currentLength == 0 ? 0 : 1) + word.Length;

                if (currentLength >= formatting.PreferredParagraphCharacters &&
                    EndsSentence(word))
                {
                    AddParagraph(paragraphs, current);
                    currentLength = 0;
                }
            }
        }

        AddParagraph(paragraphs, current);
        var text = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            paragraphs.Select(paragraph => paragraph.Text));
        return new FormattedTranscript(paragraphs, text, formatting);
    }

    private static List<TranscriptionSegment> NormalizeSegments(
        IReadOnlyList<TranscriptionSegment> source)
    {
        var result = new List<TranscriptionSegment>(source.Count);
        TimeSpan? previousStart = null;

        foreach (var segment in source)
        {
            if (segment.Start < TimeSpan.Zero ||
                segment.End < segment.Start ||
                previousStart is not null && segment.Start < previousStart)
            {
                throw new ArgumentException(
                    "Transcript segment timestamps must be non-negative, ordered, and have end not earlier than start.",
                    nameof(source));
            }

            previousStart = segment.Start;
            var text = WhitespaceRegex().Replace(segment.Text ?? string.Empty, " ").Trim();
            if (text.Length > 0)
            {
                result.Add(segment with { Text = text });
            }
        }

        return result;
    }

    private static void AppendWord(
        List<TranscriptionSegment> current,
        TranscriptionSegment source,
        string word)
    {
        if (current.Count > 0 &&
            current[^1].Start == source.Start &&
            current[^1].End == source.End)
        {
            current[^1] = current[^1] with { Text = $"{current[^1].Text} {word}" };
            return;
        }

        current.Add(source with { Text = word });
    }

    private static void AddParagraph(
        List<TranscriptParagraph> paragraphs,
        List<TranscriptionSegment> current)
    {
        if (current.Count == 0)
        {
            return;
        }

        paragraphs.Add(new TranscriptParagraph(
            string.Join(' ', current.Select(segment => segment.Text)),
            current[0].Start,
            current[^1].End));
        current.Clear();
    }

    private static bool EndsSentence(string word)
    {
        var index = word.Length - 1;
        while (index >= 0 && word[index] is '"' or '\'' or '”' or '’' or ')' or ']' or '}')
        {
            index--;
        }

        return index >= 0 && word[index] is '.' or '?' or '!';
    }

    private static void ValidateFormatting(TranscriptFormattingSnapshot formatting)
    {
        ArgumentNullException.ThrowIfNull(formatting);
        if (formatting.SilenceGapSeconds <= 0 ||
            formatting.PreferredParagraphCharacters <= 0 ||
            formatting.HardParagraphCharacters <= 0 ||
            formatting.PreferredParagraphCharacters > formatting.HardParagraphCharacters ||
            formatting.FormattingVersion !=
            Common.Options.TranscriptFormattingOptions.SupportedVersion)
        {
            throw new ArgumentException("Transcript formatting configuration is invalid.", nameof(formatting));
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

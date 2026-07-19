namespace MeetingMind.Application.Meetings;

public sealed class TranscriptChunker
{
    public IReadOnlyList<string> Split(
        string transcriptText,
        int chunkSizeCharacters,
        int overlapCharacters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcriptText);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSizeCharacters);
        ArgumentOutOfRangeException.ThrowIfNegative(overlapCharacters);

        if (overlapCharacters >= chunkSizeCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlapCharacters),
                "Chunk overlap must be smaller than the chunk size.");
        }

        var normalized = transcriptText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        if (normalized.Length <= chunkSizeCharacters)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        var start = 0;

        while (start < normalized.Length)
        {
            var desiredEnd = Math.Min(start + chunkSizeCharacters, normalized.Length);
            var end = desiredEnd == normalized.Length
                ? desiredEnd
                : FindPreferredEnd(normalized, start, desiredEnd);

            if (end <= start)
            {
                end = desiredEnd;
            }

            var chunk = normalized[start..end].Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            if (end >= normalized.Length)
            {
                break;
            }

            var overlapStart = Math.Max(start + 1, end - overlapCharacters);
            var nextStart = FindPreferredStart(normalized, overlapStart, end);
            start = nextStart > start && nextStart < end ? nextStart : end;
        }

        return chunks;
    }

    private static int FindPreferredEnd(string text, int start, int desiredEnd)
    {
        var minimumEnd = start + ((desiredEnd - start) / 2);

        var paragraph = text.LastIndexOf("\n\n", desiredEnd - 1, desiredEnd - minimumEnd, StringComparison.Ordinal);
        if (paragraph >= minimumEnd)
        {
            return paragraph + 2;
        }

        for (var index = desiredEnd - 1; index >= minimumEnd; index--)
        {
            if (IsSentenceEnd(text, index))
            {
                return index + 1;
            }
        }

        for (var index = desiredEnd - 1; index >= minimumEnd; index--)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return index + 1;
            }
        }

        return desiredEnd;
    }

    private static int FindPreferredStart(string text, int desiredStart, int previousEnd)
    {
        var paragraph = text.IndexOf("\n\n", desiredStart, previousEnd - desiredStart, StringComparison.Ordinal);
        if (paragraph >= 0)
        {
            return paragraph + 2;
        }

        for (var index = desiredStart; index < previousEnd; index++)
        {
            if (IsSentenceEnd(text, index))
            {
                return index + 1;
            }
        }

        for (var index = desiredStart; index < previousEnd; index++)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return index + 1;
            }
        }

        return desiredStart;
    }

    private static bool IsSentenceEnd(string text, int index)
    {
        return text[index] is '.' or '!' or '?' &&
               (index + 1 >= text.Length || char.IsWhiteSpace(text[index + 1]));
    }
}

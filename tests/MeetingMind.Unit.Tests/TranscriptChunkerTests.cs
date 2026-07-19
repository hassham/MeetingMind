using MeetingMind.Application.Meetings;

namespace MeetingMind.Unit.Tests;

public sealed class TranscriptChunkerTests
{
    private readonly TranscriptChunker _chunker = new();

    [Theory]
    [InlineData(59999, 1)]
    [InlineData(60000, 1)]
    [InlineData(60001, 2)]
    public void SplitHandlesConfiguredChunkBoundary(int length, int expectedChunks)
    {
        var transcript = new string('a', length);

        var chunks = _chunker.Split(transcript, 60000, 1500);

        Assert.Equal(expectedChunks, chunks.Count);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, 60000));
    }

    [Fact]
    public void SplitPrefersParagraphBoundariesBeforeSentenceAndWhitespace()
    {
        var firstParagraph = new string('a', 40);
        var transcript = firstParagraph + "\r\n\r\n" +
                         "This sentence remains together. More words continue beyond the limit.";

        var chunks = _chunker.Split(transcript, 70, 10);

        Assert.True(chunks.Count >= 2);
        Assert.Equal(firstParagraph, chunks[0]);
        Assert.Contains("This sentence remains together.", chunks[1]);
        Assert.DoesNotContain('\r', string.Concat(chunks));
    }

    [Fact]
    public void SplitUsesBoundedOverlapWithoutLosingTranscriptTail()
    {
        var transcript = string.Join(' ', Enumerable.Range(1, 40).Select(index => $"word{index}"));

        var chunks = _chunker.Split(transcript, 80, 20);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, 80));
        Assert.Contains("word40", chunks[^1]);

        for (var index = 1; index < chunks.Count; index++)
        {
            var priorWords = chunks[index - 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var currentWords = chunks[index].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.Contains(currentWords[0], priorWords);
        }
    }

    [Fact]
    public void SplitRejectsOverlapThatCannotMakeProgress()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _chunker.Split("some transcript", 10, 10));
    }
}

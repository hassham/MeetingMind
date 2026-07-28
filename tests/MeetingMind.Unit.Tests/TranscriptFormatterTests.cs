using MeetingMind.Application.Meetings;

namespace MeetingMind.Unit.Tests;

public sealed class TranscriptFormatterTests
{
    private static readonly TranscriptFormattingSnapshot Defaults =
        new(1.5, 300, 700, "v1");

    private readonly TranscriptFormatter _formatter = new();

    [Fact]
    public void NormalizesWhitespaceWithoutChangingWordOrder()
    {
        var result = _formatter.Format(
            new TranscriptionResult(
            [
                Segment(0, 1, "  Alpha\t beta "),
                Segment(1, 2, "\r\n gamma   delta")
            ]),
            Defaults);

        var paragraph = Assert.Single(result.Paragraphs);
        Assert.Equal("Alpha beta gamma delta", paragraph.Text);
        Assert.Equal(TimeSpan.Zero, paragraph.Start);
        Assert.Equal(TimeSpan.FromSeconds(2), paragraph.End);
        Assert.Equal(paragraph.Text, result.Text);
    }

    [Fact]
    public void SilenceGapStartsNewParagraph()
    {
        var result = _formatter.Format(
            new TranscriptionResult(
            [
                Segment(0, 1, "First thought"),
                Segment(2.5, 3, "Second thought")
            ]),
            Defaults);

        Assert.Equal(["First thought", "Second thought"], result.Paragraphs.Select(x => x.Text));
    }

    [Fact]
    public void PreferredSizeBreaksOnlyAtSentenceEndingPunctuation()
    {
        var options = Defaults with
        {
            PreferredParagraphCharacters = 12,
            HardParagraphCharacters = 50
        };
        var result = _formatter.Format(
            new TranscriptionResult(
            [
                Segment(0, 1, "One two three"),
                Segment(1, 2, "four five!"),
                Segment(2, 3, "Next sentence")
            ]),
            options);

        Assert.Equal(
            ["One two three four five!", "Next sentence"],
            result.Paragraphs.Select(x => x.Text));
    }

    [Fact]
    public void SentenceEndingAllowsClosingQuotesAndBrackets()
    {
        var options = Defaults with
        {
            PreferredParagraphCharacters = 5,
            HardParagraphCharacters = 50
        };
        var result = _formatter.Format(
            new TranscriptionResult(
            [
                Segment(0, 1, "Done!\")"),
                Segment(1, 2, "Next")
            ]),
            options);

        Assert.Equal(["Done!\")", "Next"], result.Paragraphs.Select(x => x.Text));
    }

    [Fact]
    public void HardLimitBreaksBeforeOverflow()
    {
        var options = Defaults with
        {
            PreferredParagraphCharacters = 15,
            HardParagraphCharacters = 15
        };
        var result = _formatter.Format(
            new TranscriptionResult([Segment(0, 1, "12345 67890 abcde")]),
            options);

        Assert.Equal(["12345 67890", "abcde"], result.Paragraphs.Select(x => x.Text));
        Assert.All(result.Paragraphs, paragraph => Assert.True(paragraph.Text.Length <= 15));
    }

    [Fact]
    public void IndivisibleOverLimitTokenGetsItsOwnParagraph()
    {
        var options = Defaults with
        {
            PreferredParagraphCharacters = 10,
            HardParagraphCharacters = 10
        };
        var longToken = new string('x', 11);
        var result = _formatter.Format(
            new TranscriptionResult([Segment(0, 1, $"before {longToken} after")]),
            options);

        Assert.Equal(["before", longToken, "after"], result.Paragraphs.Select(x => x.Text));
    }

    [Fact]
    public void EmptySegmentsProduceEmptyTranscript()
    {
        var result = _formatter.Format(
            new TranscriptionResult([Segment(0, 1, " \t "), Segment(1, 2, "")]),
            Defaults);

        Assert.Empty(result.Paragraphs);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact]
    public void IdenticalInputProducesIdenticalOutput()
    {
        var input = new TranscriptionResult(
            [Segment(0, 1, "Stable input."), Segment(1, 2, "Stable output.")]);

        var first = _formatter.Format(input, Defaults);
        var second = _formatter.Format(input, Defaults);

        Assert.Equal(first.Text, second.Text);
        Assert.Equal(first.Formatting, second.Formatting);
        Assert.Equal(first.Paragraphs, second.Paragraphs);
    }

    [Theory]
    [InlineData(-1, 300, 700)]
    [InlineData(1.5, 0, 700)]
    [InlineData(1.5, 701, 700)]
    public void InvalidFormattingIsRejected(
        double silenceGapSeconds,
        int preferredCharacters,
        int hardCharacters)
    {
        Assert.Throws<ArgumentException>(() =>
            _formatter.Format(
                new TranscriptionResult([]),
                new TranscriptFormattingSnapshot(
                    silenceGapSeconds,
                    preferredCharacters,
                    hardCharacters,
                    "v1")));
    }

    [Fact]
    public void InvalidOrNonMonotonicTimestampsAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            _formatter.Format(
                new TranscriptionResult(
                [
                    Segment(2, 3, "Later"),
                    Segment(1, 2, "Earlier")
                ]),
                Defaults));
    }

    private static TranscriptionSegment Segment(double start, double end, string text) =>
        new(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), text);
}

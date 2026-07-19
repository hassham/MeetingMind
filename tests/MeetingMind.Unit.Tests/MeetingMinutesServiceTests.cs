using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Meetings;
using System.Text.Json;

namespace MeetingMind.Unit.Tests;

public sealed class MeetingMinutesServiceTests
{
    [Fact]
    public async Task ShortTranscriptUsesExactlyOneProviderCall()
    {
        var harness = new GenerationHarness();
        var progress = new List<MeetingMinutesGenerationProgress>();

        var result = await harness.Service.GenerateMinutesAsync(
            "Short complete transcript",
            CancellationToken.None,
            (update, _) =>
            {
                progress.Add(update);
                return Task.CompletedTask;
            });

        Assert.Equal("Short complete transcript", Assert.Single(harness.Client.TranscriptCalls).Text);
        Assert.Empty(harness.Client.AggregationCalls);
        Assert.Equal("Meeting 1", result.Title);
        Assert.Equal(89, Assert.Single(progress).Percent);
    }

    [Fact]
    public async Task LongTranscriptGeneratesSequentialPartialsAndPreservesAllSections()
    {
        var harness = new GenerationHarness();
        var progress = new List<MeetingMinutesGenerationProgress>();
        var transcript = CreateLongTranscript(30);

        var result = await harness.Service.GenerateMinutesAsync(
            transcript,
            CancellationToken.None,
            (update, _) =>
            {
                progress.Add(update);
                return Task.CompletedTask;
            });

        Assert.True(harness.Client.TranscriptCalls.Count > 1);
        Assert.Equal(
            Enumerable.Range(1, harness.Client.TranscriptCalls.Count),
            harness.Client.TranscriptCalls.Select(call => call.ChunkNumber));
        Assert.Single(harness.Client.AggregationCalls);
        Assert.Equal("Meeting 1", result.Title);
        Assert.Contains("Summary 1", result.Summary);
        Assert.Equal(harness.Client.TranscriptCalls.Count, result.Decisions.Count);
        Assert.Single(result.Attendees);
        Assert.Equal(harness.Client.TranscriptCalls.Count, result.DiscussionPoints.Count);
        Assert.Equal(harness.Client.TranscriptCalls.Count, result.ActionItems.Count);
        Assert.Equal(harness.Client.TranscriptCalls.Count, result.Risks.Count);
        Assert.Equal(harness.Client.TranscriptCalls.Count, result.NextSteps.Count);
        Assert.Contains(result.ActionItems, item => item.Description == "Action 1");
        Assert.Equal(85, progress.Last(update => update.Phase == "partial-generation").Percent);
        Assert.Equal(89, progress[^1].Percent);
        Assert.Equal(progress.OrderBy(update => update.Percent).Select(update => update.Percent),
            progress.Select(update => update.Percent));
    }

    [Fact]
    public async Task AggregationUsesMultipleBoundedTiersWhenOneGroupCannotFit()
    {
        var client = new StubGenerationClient
        {
            AggregateFactory = call => CreateCompactMinutes($"Tier {call.Tier} group {call.GroupNumber}")
        };
        var merger = new MeetingMinutesMerger();
        var twoPartials = merger.Merge([CreateMinutes(1), CreateMinutes(2)]);
        var options = CreateOptions();
        options.MaxAggregationInputCharacters = JsonSerializer.Serialize(
            twoPartials,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length;
        var service = CreateService(client, options, merger);

        await service.GenerateMinutesAsync(CreateLongTranscript(60), CancellationToken.None);

        Assert.True(client.TranscriptCalls.Count >= 4);
        Assert.True(client.AggregationCalls.Select(call => call.Tier).Distinct().Count() >= 2);
        Assert.All(
            client.AggregationCalls.GroupBy(call => call.Tier),
            tier => Assert.Equal(
                Enumerable.Range(1, tier.Count()),
                tier.Select(call => call.GroupNumber)));
    }

    [Fact]
    public async Task OversizedTranscriptFailsPermanentlyWithoutProviderCalls()
    {
        var harness = new GenerationHarness();
        var transcript = new string('x', harness.Options.MaxTranscriptCharacters + 1);

        var exception = await Assert.ThrowsAsync<PermanentMeetingProcessingException>(() =>
            harness.Service.GenerateMinutesAsync(transcript, CancellationToken.None));

        Assert.Contains("configured maximum", exception.Message);
        Assert.Empty(harness.Client.TranscriptCalls);
        Assert.Empty(harness.Client.AggregationCalls);
    }

    [Fact]
    public async Task PartialFailureStopsAttemptAndPropagatesForHangfireRetry()
    {
        var harness = new GenerationHarness();
        harness.Client.FailTranscriptCall = 2;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.GenerateMinutesAsync(CreateLongTranscript(30), CancellationToken.None));

        Assert.Equal("temporary provider failure", exception.Message);
        Assert.Equal(2, harness.Client.TranscriptCalls.Count);
        Assert.Empty(harness.Client.AggregationCalls);
    }

    [Fact]
    public async Task PartialResultAboveAggregationLimitFailsPermanently()
    {
        var options = CreateOptions();
        options.MaxAggregationInputCharacters = 10;
        var service = CreateService(new StubGenerationClient(), options, new MeetingMinutesMerger());

        var exception = await Assert.ThrowsAsync<PermanentMeetingProcessingException>(() =>
            service.GenerateMinutesAsync(CreateLongTranscript(30), CancellationToken.None));

        Assert.Contains("aggregation input limit", exception.Message);
    }

    private static MeetingMinutesService CreateService(
        IMeetingMinutesGenerationClient client,
        MeetingMinutesGenerationOptions options,
        MeetingMinutesMerger merger)
    {
        return new MeetingMinutesService(client, options, new TranscriptChunker(), merger);
    }

    private static MeetingMinutesGenerationOptions CreateOptions()
    {
        return new MeetingMinutesGenerationOptions
        {
            SinglePassMaxCharacters = 80,
            ChunkSizeCharacters = 40,
            ChunkOverlapCharacters = 8,
            MaxTranscriptCharacters = 1000,
            MaxAggregationInputCharacters = 10000
        };
    }

    private static string CreateLongTranscript(int wordCount) =>
        string.Join(' ', Enumerable.Range(1, wordCount).Select(index => $"word{index}"));

    private static MeetingMinutesContent CreateMinutes(int number)
    {
        return new MeetingMinutesContent(
            $"Meeting {number}",
            $"Summary {number}",
            ["Hasham"],
            [$"Discussion {number}"],
            [$"Decision {number}"],
            [new MeetingActionItem($"Action {number}", number % 2 == 0 ? "Hasham" : null, null)],
            [$"Risk {number}"],
            [$"Next step {number}"]);
    }

    private static MeetingMinutesContent CreateCompactMinutes(string label)
    {
        return new MeetingMinutesContent(label, label, [], [], [], [], [], []);
    }

    private sealed class GenerationHarness
    {
        public GenerationHarness()
        {
            Service = CreateService(Client, Options, new MeetingMinutesMerger());
        }

        public StubGenerationClient Client { get; } = new();

        public MeetingMinutesGenerationOptions Options { get; } = CreateOptions();

        public MeetingMinutesService Service { get; }
    }

    private sealed class StubGenerationClient : IMeetingMinutesGenerationClient
    {
        public List<TranscriptCall> TranscriptCalls { get; } = [];

        public List<AggregationCall> AggregationCalls { get; } = [];

        public int? FailTranscriptCall { get; set; }

        public Func<AggregationCall, MeetingMinutesContent>? AggregateFactory { get; set; }

        public Task<MeetingMinutesContent> GenerateFromTranscriptAsync(
            string transcriptText,
            int chunkNumber,
            int chunkCount,
            CancellationToken cancellationToken)
        {
            TranscriptCalls.Add(new TranscriptCall(transcriptText, chunkNumber, chunkCount));
            if (FailTranscriptCall == TranscriptCalls.Count)
            {
                return Task.FromException<MeetingMinutesContent>(
                    new InvalidOperationException("temporary provider failure"));
            }

            return Task.FromResult(CreateMinutes(chunkNumber));
        }

        public Task<MeetingMinutesContent> AggregateAsync(
            MeetingMinutesContent mergedMinutes,
            int tier,
            int groupNumber,
            int groupCount,
            CancellationToken cancellationToken)
        {
            var call = new AggregationCall(mergedMinutes, tier, groupNumber, groupCount);
            AggregationCalls.Add(call);
            return Task.FromResult(AggregateFactory?.Invoke(call) ?? mergedMinutes);
        }
    }

    private sealed record TranscriptCall(string Text, int ChunkNumber, int ChunkCount);

    private sealed record AggregationCall(
        MeetingMinutesContent Minutes,
        int Tier,
        int GroupNumber,
        int GroupCount);
}

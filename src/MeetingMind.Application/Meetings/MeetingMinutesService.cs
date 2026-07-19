using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using System.Text.Json;

namespace MeetingMind.Application.Meetings;

public sealed class MeetingMinutesService : IMeetingMinutesService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMeetingMinutesGenerationClient _generationClient;
    private readonly MeetingMinutesGenerationOptions _options;
    private readonly TranscriptChunker _chunker;
    private readonly MeetingMinutesMerger _merger;

    public MeetingMinutesService(
        IMeetingMinutesGenerationClient generationClient,
        MeetingMinutesGenerationOptions options,
        TranscriptChunker chunker,
        MeetingMinutesMerger merger)
    {
        _generationClient = generationClient;
        _options = options;
        _chunker = chunker;
        _merger = merger;
    }

    public async Task<MeetingMinutesContent> GenerateMinutesAsync(
        string transcriptText,
        CancellationToken cancellationToken,
        Func<MeetingMinutesGenerationProgress, CancellationToken, Task>? progressCallback = null)
    {
        if (string.IsNullOrWhiteSpace(transcriptText))
        {
            throw new PermanentMeetingProcessingException(
                "Transcript is empty; meeting minutes cannot be generated.");
        }

        var normalizedTranscript = transcriptText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        if (normalizedTranscript.Length > _options.MaxTranscriptCharacters)
        {
            throw new PermanentMeetingProcessingException(
                $"Transcript exceeds the configured maximum of {_options.MaxTranscriptCharacters} characters.");
        }

        if (normalizedTranscript.Length <= _options.SinglePassMaxCharacters)
        {
            var result = await _generationClient.GenerateFromTranscriptAsync(
                normalizedTranscript,
                chunkNumber: 1,
                chunkCount: 1,
                cancellationToken);

            await ReportProgressAsync(progressCallback, 89, "single-pass", 1, 1, cancellationToken);
            return _merger.Merge([result]);
        }

        var chunks = _chunker.Split(
            normalizedTranscript,
            _options.ChunkSizeCharacters,
            _options.ChunkOverlapCharacters);
        var partials = new List<MeetingMinutesContent>(chunks.Count);

        for (var index = 0; index < chunks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var partial = await _generationClient.GenerateFromTranscriptAsync(
                chunks[index],
                index + 1,
                chunks.Count,
                cancellationToken);
            partials.Add(_merger.Merge([partial]));

            var percent = 60 + (int)Math.Floor(25d * (index + 1) / chunks.Count);
            await ReportProgressAsync(
                progressCallback,
                percent,
                "partial-generation",
                index + 1,
                chunks.Count,
                cancellationToken);
        }

        return await AggregateAsync(partials, progressCallback, cancellationToken);
    }

    private async Task<MeetingMinutesContent> AggregateAsync(
        IReadOnlyList<MeetingMinutesContent> partials,
        Func<MeetingMinutesGenerationProgress, CancellationToken, Task>? progressCallback,
        CancellationToken cancellationToken)
    {
        var current = partials.ToList();
        var initialCount = current.Count;
        var tier = 1;

        await ReportProgressAsync(
            progressCallback,
            86,
            "aggregation",
            0,
            Math.Max(1, initialCount - 1),
            cancellationToken);

        while (current.Count > 1)
        {
            var groups = CreateAggregationGroups(current);
            if (groups.Count >= current.Count)
            {
                throw new PermanentMeetingProcessingException(
                    "Structured partial results cannot be combined within the configured aggregation input limit.");
            }

            var next = new List<MeetingMinutesContent>(groups.Count);
            for (var index = 0; index < groups.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var aggregate = await _generationClient.AggregateAsync(
                    groups[index],
                    tier,
                    index + 1,
                    groups.Count,
                    cancellationToken);
                next.Add(_merger.Merge([aggregate]));
            }

            current = next;
            var completedReduction = initialCount - current.Count;
            var requiredReduction = Math.Max(1, initialCount - 1);
            var percent = 86 + (int)Math.Floor(3d * completedReduction / requiredReduction);
            await ReportProgressAsync(
                progressCallback,
                Math.Min(89, percent),
                "aggregation",
                completedReduction,
                requiredReduction,
                cancellationToken);
            tier++;
        }

        return _merger.Merge(current);
    }

    private IReadOnlyList<MeetingMinutesContent> CreateAggregationGroups(
        IReadOnlyList<MeetingMinutesContent> minutes)
    {
        var groups = new List<MeetingMinutesContent>();
        var pending = new List<MeetingMinutesContent>();

        foreach (var item in minutes)
        {
            var candidate = pending.Append(item).ToArray();
            var mergedCandidate = _merger.Merge(candidate);
            if (GetSerializedLength(mergedCandidate) <= _options.MaxAggregationInputCharacters)
            {
                pending.Add(item);
                continue;
            }

            if (pending.Count == 0)
            {
                throw new PermanentMeetingProcessingException(
                    "A structured partial result exceeds the configured aggregation input limit.");
            }

            groups.Add(_merger.Merge(pending));
            pending.Clear();
            pending.Add(item);

            if (GetSerializedLength(_merger.Merge(pending)) > _options.MaxAggregationInputCharacters)
            {
                throw new PermanentMeetingProcessingException(
                    "A structured partial result exceeds the configured aggregation input limit.");
            }
        }

        if (pending.Count > 0)
        {
            groups.Add(_merger.Merge(pending));
        }

        return groups;
    }

    private static int GetSerializedLength(MeetingMinutesContent minutes) =>
        JsonSerializer.Serialize(minutes, JsonOptions).Length;

    private static Task ReportProgressAsync(
        Func<MeetingMinutesGenerationProgress, CancellationToken, Task>? progressCallback,
        int percent,
        string phase,
        int completed,
        int total,
        CancellationToken cancellationToken)
    {
        return progressCallback?.Invoke(
                   new MeetingMinutesGenerationProgress(percent, phase, completed, total),
                   cancellationToken)
               ?? Task.CompletedTask;
    }
}

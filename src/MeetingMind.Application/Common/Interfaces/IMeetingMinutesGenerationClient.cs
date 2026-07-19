using MeetingMind.Application.Meetings;

namespace MeetingMind.Application.Common.Interfaces;

public interface IMeetingMinutesGenerationClient
{
    Task<MeetingMinutesContent> GenerateFromTranscriptAsync(
        string transcriptText,
        int chunkNumber,
        int chunkCount,
        CancellationToken cancellationToken);

    Task<MeetingMinutesContent> AggregateAsync(
        MeetingMinutesContent mergedMinutes,
        int tier,
        int groupNumber,
        int groupCount,
        CancellationToken cancellationToken);
}

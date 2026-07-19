namespace MeetingMind.Application.Common.Options;

public class MeetingMinutesGenerationOptions
{
    public int SinglePassMaxCharacters { get; set; } = 120000;

    public int ChunkSizeCharacters { get; set; } = 60000;

    public int ChunkOverlapCharacters { get; set; } = 1500;

    public int MaxTranscriptCharacters { get; set; } = 1200000;

    public int MaxAggregationInputCharacters { get; set; } = 120000;
}

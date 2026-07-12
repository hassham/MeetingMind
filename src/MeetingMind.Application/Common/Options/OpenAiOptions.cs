namespace MeetingMind.Application.Common.Options;

public class OpenAiOptions
{
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gpt-4.1";

    public int MaxTranscriptCharactersForMinutes { get; set; } = 120000;
}

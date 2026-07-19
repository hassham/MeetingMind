namespace MeetingMind.Application.Common.Options;

public class OpenAiOptions
{
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gpt-4.1";
}

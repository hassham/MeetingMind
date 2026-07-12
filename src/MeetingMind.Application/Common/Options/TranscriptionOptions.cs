namespace MeetingMind.Application.Common.Options;

public class TranscriptionOptions
{
    public string? ModelPath { get; set; }

    public string ModelDirectory { get; set; } = Path.Combine("Models", "Whisper");

    public string ModelSize { get; set; } = "small";

    public bool AutoDownloadModel { get; set; } = true;

    public string Language { get; set; } = "auto";
}

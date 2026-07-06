namespace MeetingMind.Application.Common.Options;

public class StorageOptions
{
    public string RootPath { get; set; } = "Storage";

    public int MaxUploadSizeMb { get; set; } = 100;

    public string[] AllowedExtensions { get; set; } = [".mp3", ".wav", ".m4a", ".aac"];

    public string OriginalAudioFolder { get; set; } = Path.Combine("Audio", "Original");

    public string ProcessedAudioFolder { get; set; } = Path.Combine("Audio", "Processed");

    public string TranscriptFolder { get; set; } = "Transcript";

    public string MinutesFolder { get; set; } = "Minutes";

    public long MaxUploadSizeBytes => MaxUploadSizeMb * 1024L * 1024L;
}

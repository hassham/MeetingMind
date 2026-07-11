namespace MeetingMind.Application.Common.Options;

public class AudioProcessingOptions
{
    public string? FfmpegBinaryFolder { get; set; }

    public string OutputExtension { get; set; } = ".mp3";

    public string OutputFormat { get; set; } = "mp3";

    public string AudioCodec { get; set; } = "libmp3lame";

    public int SampleRate { get; set; } = 16000;

    public int Channels { get; set; } = 1;

    public int AudioBitrateKbps { get; set; } = 64;
}

namespace MeetingMind.Application.Common.Options;

public class AudioProcessingOptions
{
    public string? FfmpegBinaryFolder { get; set; }

    public string OutputExtension { get; set; } = ".wav";

    public string OutputFormat { get; set; } = "wav";

    public string AudioCodec { get; set; } = "pcm_s16le";

    public int SampleRate { get; set; } = 16000;

    public int Channels { get; set; } = 1;

    public int? AudioBitrateKbps { get; set; }
}

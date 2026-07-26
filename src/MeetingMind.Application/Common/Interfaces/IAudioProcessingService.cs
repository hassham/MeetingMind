namespace MeetingMind.Application.Common.Interfaces;

public interface IAudioProcessingService
{
    Task<AudioProcessingResult> ConvertToStandardFormatAsync(
        string inputPath,
        CancellationToken cancellationToken);
}

public sealed record AudioProcessingResult(
    string ProcessedFilePath,
    long SourceDurationSeconds);

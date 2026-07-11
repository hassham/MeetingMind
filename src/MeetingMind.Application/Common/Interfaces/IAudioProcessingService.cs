namespace MeetingMind.Application.Common.Interfaces;

public interface IAudioProcessingService
{
    Task<string> ConvertToStandardFormatAsync(string inputPath, CancellationToken cancellationToken);
}

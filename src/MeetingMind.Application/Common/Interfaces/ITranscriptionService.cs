namespace MeetingMind.Application.Common.Interfaces;

public interface ITranscriptionService
{
    Task<string> TranscribeAsync(string audioPath, CancellationToken cancellationToken);
}

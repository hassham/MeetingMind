namespace MeetingMind.Application.Common.Interfaces;

public interface ITranscriptionService
{
    Task<MeetingMind.Application.Meetings.TranscriptionResult> TranscribeAsync(
        string audioPath,
        CancellationToken cancellationToken);
}

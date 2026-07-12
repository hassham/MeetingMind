namespace MeetingMind.Application.Meetings;

public interface IMeetingTranscriptService
{
    Task<MeetingTranscriptDownloadResult?> GetTranscriptDownloadAsync(
        Guid jobId,
        CancellationToken cancellationToken);
}

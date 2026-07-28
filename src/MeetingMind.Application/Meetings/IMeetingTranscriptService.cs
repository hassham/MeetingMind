namespace MeetingMind.Application.Meetings;

public interface IMeetingTranscriptService
{
    Task<MeetingTranscriptResult?> GetTranscriptAsync(
        Guid jobId,
        CancellationToken cancellationToken);

    Task<MeetingTranscriptDownloadResult?> GetTranscriptDownloadAsync(
        Guid jobId,
        CancellationToken cancellationToken);
}

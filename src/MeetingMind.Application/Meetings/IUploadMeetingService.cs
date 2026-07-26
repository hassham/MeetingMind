namespace MeetingMind.Application.Meetings;

public interface IUploadMeetingService
{
    Task<UploadMeetingResult> UploadAsync(UploadMeetingRequest request, CancellationToken cancellationToken);

    Task<UploadMeetingResult> UploadTranscriptAsync(
        UploadMeetingRequest request,
        CancellationToken cancellationToken);
}

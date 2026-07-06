namespace MeetingMind.Application.Meetings;

public interface IUploadMeetingService
{
    Task<UploadMeetingResult> UploadAsync(UploadMeetingRequest request, CancellationToken cancellationToken);
}

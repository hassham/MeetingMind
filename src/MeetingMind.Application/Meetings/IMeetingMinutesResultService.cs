namespace MeetingMind.Application.Meetings;

public interface IMeetingMinutesResultService
{
    Task<MeetingMinutesResult?> GetMinutesAsync(Guid meetingJobId, CancellationToken cancellationToken);

    Task<MeetingMinutesDownloadResult?> GetMinutesDownloadAsync(
        Guid meetingJobId,
        CancellationToken cancellationToken);
}

namespace MeetingMind.Application.Meetings;

public interface IMeetingRetryService
{
    Task<MeetingRetryResult> RetryAsync(Guid meetingJobId, CancellationToken cancellationToken);
}

namespace MeetingMind.Application.Meetings;

public interface IMeetingStatusService
{
    Task<MeetingJobStatusResult?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken);
}

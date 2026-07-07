namespace MeetingMind.Application.Common.Interfaces;

public interface IMeetingProcessingJob
{
    Task ProcessMeetingAsync(Guid jobId);
}

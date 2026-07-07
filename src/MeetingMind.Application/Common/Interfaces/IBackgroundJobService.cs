namespace MeetingMind.Application.Common.Interfaces;

public interface IBackgroundJobService
{
    string EnqueueMeetingProcessing(Guid jobId);
}

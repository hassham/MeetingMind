using Hangfire;
using MeetingMind.Application.Common.Interfaces;

namespace MeetingMind.Infrastructure.BackgroundJobs;

public class HangfireBackgroundJobService : IBackgroundJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireBackgroundJobService(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public string EnqueueMeetingProcessing(Guid jobId)
    {
        return _backgroundJobClient.Enqueue<IMeetingProcessingJob>(
            job => job.ProcessMeetingAsync(jobId));
    }
}

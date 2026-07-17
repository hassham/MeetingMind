using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Unit.Tests;

public class MeetingJobTests
{
    [Fact]
    public void NewMeetingJobDefaultsToQueuedUploadState()
    {
        var job = new MeetingJob();

        Assert.Equal(MeetingJobStatus.Queued, job.Status);
        Assert.Equal(MeetingJobStage.Uploaded, job.Stage);
        Assert.Equal(0, job.Progress);
        Assert.NotEqual(default, job.CreatedAt);
        Assert.NotEqual(default, job.UpdatedAt);
    }
}

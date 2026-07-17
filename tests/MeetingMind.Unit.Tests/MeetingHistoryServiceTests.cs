using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Unit.Tests;

public class MeetingHistoryServiceTests
{
    [Fact]
    public async Task GetHistoryAsyncNormalizesPaginationAndMapsJobs()
    {
        var jobId = Guid.NewGuid();
        var repository = new RetryStubMeetingJobRepository
        {
            History =
            [
                new MeetingJob
                {
                    Id = jobId,
                    OriginalFileName = "meeting.mp3",
                    Status = MeetingJobStatus.Failed,
                    Stage = MeetingJobStage.GeneratingMinutes,
                    Progress = 60,
                    ErrorMessage = "OpenAI failed"
                }
            ],
            Total = 125
        };
        var service = new MeetingHistoryService(repository);

        var result = await service.GetHistoryAsync(-5, 500, CancellationToken.None);

        Assert.Equal(0, result.Skip);
        Assert.Equal(100, result.Take);
        Assert.Equal(125, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(jobId, result.Items[0].JobId);
        Assert.Equal("meeting.mp3", result.Items[0].OriginalFileName);
        Assert.Equal("Failed", result.Items[0].Status);
        Assert.Equal("GeneratingMinutes", result.Items[0].Stage);
    }
}

using MeetingMind.Application.Common.Interfaces;

namespace MeetingMind.Application.Meetings;

public class MeetingStatusService : IMeetingStatusService
{
    private readonly IMeetingJobRepository _meetingJobRepository;

    public MeetingStatusService(IMeetingJobRepository meetingJobRepository)
    {
        _meetingJobRepository = meetingJobRepository;
    }

    public async Task<MeetingJobStatusResult?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var meetingJob = await _meetingJobRepository.GetByIdAsync(jobId, cancellationToken);
        if (meetingJob is null)
        {
            return null;
        }

        return new MeetingJobStatusResult(
            meetingJob.Id,
            meetingJob.Status.ToString(),
            meetingJob.Stage.ToString(),
            meetingJob.Progress,
            meetingJob.ErrorMessage);
    }
}

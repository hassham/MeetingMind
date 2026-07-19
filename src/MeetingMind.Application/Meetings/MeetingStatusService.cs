using MeetingMind.Application.Common.Interfaces;

namespace MeetingMind.Application.Meetings;

public class MeetingStatusService : IMeetingStatusService
{
    private readonly IMeetingJobRepository _meetingJobRepository;
    private readonly TimeProvider _timeProvider;

    public MeetingStatusService(
        IMeetingJobRepository meetingJobRepository,
        TimeProvider timeProvider)
    {
        _meetingJobRepository = meetingJobRepository;
        _timeProvider = timeProvider;
    }

    public async Task<MeetingJobStatusResult?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var meetingJob = await _meetingJobRepository.GetByIdAsync(jobId, cancellationToken);
        if (meetingJob is null)
        {
            return null;
        }

        var duration = MeetingDurationCalculator.Calculate(meetingJob, _timeProvider.GetUtcNow());

        return new MeetingJobStatusResult(
            meetingJob.Id,
            meetingJob.Status.ToString(),
            meetingJob.Stage.ToString(),
            meetingJob.Progress,
            meetingJob.ErrorCode,
            meetingJob.ErrorMessage,
            meetingJob.AutomaticRetryCount,
            meetingJob.AutomaticRetryLimit,
            meetingJob.NextRetryAt,
            duration.ProcessingDurationSeconds,
            duration.TotalDurationSeconds);
    }
}

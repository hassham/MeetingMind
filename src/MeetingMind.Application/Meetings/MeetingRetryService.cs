using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Application.Meetings;

public class MeetingRetryService : IMeetingRetryService
{
    private readonly IMeetingJobRepository _meetingJobRepository;
    private readonly IBackgroundJobService _backgroundJobService;

    public MeetingRetryService(
        IMeetingJobRepository meetingJobRepository,
        IBackgroundJobService backgroundJobService)
    {
        _meetingJobRepository = meetingJobRepository;
        _backgroundJobService = backgroundJobService;
    }

    public async Task<MeetingRetryResult> RetryAsync(
        Guid meetingJobId,
        CancellationToken cancellationToken)
    {
        var meetingJob = await _meetingJobRepository.GetByIdAsync(meetingJobId, cancellationToken);
        if (meetingJob is null)
        {
            return new MeetingRetryResult(
                meetingJobId,
                string.Empty,
                string.Empty,
                MeetingRetryFailureReason.NotFound);
        }

        if (meetingJob.Status is not (MeetingJobStatus.Failed or MeetingJobStatus.Cancelled))
        {
            return new MeetingRetryResult(
                meetingJobId,
                meetingJob.Status.ToString(),
                meetingJob.Stage.ToString(),
                MeetingRetryFailureReason.NotRetryable);
        }

        await _meetingJobRepository.ResetForRetryAsync(meetingJobId, cancellationToken);
        var hangfireJobId = _backgroundJobService.EnqueueMeetingProcessing(meetingJobId);
        await _meetingJobRepository.SetHangfireJobIdAsync(meetingJobId, hangfireJobId, cancellationToken);

        return new MeetingRetryResult(
            meetingJobId,
            MeetingJobStatus.Queued.ToString(),
            meetingJob.ProcessingMode.InitialStage().ToString());
    }
}

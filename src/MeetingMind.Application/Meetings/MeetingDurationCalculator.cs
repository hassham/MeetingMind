using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Application.Meetings;

public static class MeetingDurationCalculator
{
    public static MeetingDuration Calculate(MeetingJob meetingJob, DateTimeOffset now)
    {
        var isTerminal = meetingJob.Status is
            MeetingJobStatus.Completed or MeetingJobStatus.Failed or MeetingJobStatus.Cancelled;
        var totalEnd = isTerminal
            ? meetingJob.CompletedAt ?? meetingJob.UpdatedAt
            : now;

        var processingDuration = CalculateProcessingDuration(meetingJob, now, isTerminal);
        var totalDuration = ToWholeSeconds(totalEnd - meetingJob.CreatedAt);

        return new MeetingDuration(processingDuration, totalDuration);
    }

    private static long CalculateProcessingDuration(
        MeetingJob meetingJob,
        DateTimeOffset now,
        bool isTerminal)
    {
        if (meetingJob.StartedAt is null)
        {
            return 0;
        }

        DateTimeOffset processingEnd;
        if (meetingJob.Status == MeetingJobStatus.Processing)
        {
            processingEnd = now;
        }
        else if (meetingJob.Status == MeetingJobStatus.Queued &&
                 meetingJob.AutomaticRetryCount > 0 &&
                 meetingJob.NextRetryAt is not null)
        {
            processingEnd = now;
        }
        else if (isTerminal)
        {
            processingEnd = meetingJob.CompletedAt ?? meetingJob.UpdatedAt;
        }
        else if (meetingJob.Status == MeetingJobStatus.Queued && meetingJob.CompletedAt is not null)
        {
            processingEnd = meetingJob.CompletedAt.Value;
        }
        else
        {
            return 0;
        }

        return ToWholeSeconds(processingEnd - meetingJob.StartedAt.Value);
    }

    private static long ToWholeSeconds(TimeSpan duration)
    {
        return Math.Max(0, (long)Math.Floor(duration.TotalSeconds));
    }
}

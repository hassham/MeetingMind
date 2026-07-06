using MeetingMind.Domain.Enums;

namespace MeetingMind.Application.Meetings;

public sealed record UploadMeetingResult(
    Guid JobId,
    MeetingJobStatus Status,
    MeetingJobStage Stage);

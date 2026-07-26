using MeetingMind.Domain.Enums;

namespace MeetingMind.Application.Meetings;

public sealed record UploadMeetingResult(
    Guid JobId,
    MeetingProcessingMode ProcessingMode,
    MeetingJobStatus Status,
    MeetingJobStage Stage);

namespace MeetingMind.Domain.Enums;

public enum MeetingJobStage
{
    Uploaded = 0,
    Validating = 1,
    Transcoding = 2,
    Transcribing = 3,
    GeneratingMinutes = 4,
    SavingResults = 5,
    Completed = 6,
    Failed = 7
}

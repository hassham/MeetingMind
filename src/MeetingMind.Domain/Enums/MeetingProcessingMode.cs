namespace MeetingMind.Domain.Enums;

public enum MeetingProcessingMode
{
    TranscriptOnly,
    FullMeeting,
    MinutesFromTranscript
}

public static class MeetingProcessingModeExtensions
{
    public static bool RequiresAudio(this MeetingProcessingMode mode)
    {
        return mode is MeetingProcessingMode.TranscriptOnly or MeetingProcessingMode.FullMeeting;
    }

    public static bool GeneratesMinutes(this MeetingProcessingMode mode)
    {
        return mode is MeetingProcessingMode.FullMeeting or MeetingProcessingMode.MinutesFromTranscript;
    }

    public static MeetingJobStage InitialStage(this MeetingProcessingMode mode)
    {
        return mode == MeetingProcessingMode.MinutesFromTranscript
            ? MeetingJobStage.GeneratingMinutes
            : MeetingJobStage.Uploaded;
    }
}

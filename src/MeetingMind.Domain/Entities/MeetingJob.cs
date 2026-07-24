using MeetingMind.Domain.Enums;

namespace MeetingMind.Domain.Entities;

public class MeetingJob
{
    public Guid Id { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string OriginalFilePath { get; set; } = string.Empty;

    public string? ProcessedFilePath { get; set; }

    public MeetingProcessingMode ProcessingMode { get; set; } = MeetingProcessingMode.FullMeeting;

    public long? SourceAudioDurationSeconds { get; set; }

    public MeetingJobStatus Status { get; set; } = MeetingJobStatus.Queued;

    public MeetingJobStage Stage { get; set; } = MeetingJobStage.Uploaded;

    public int Progress { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? HangfireJobId { get; set; }

    public int AutomaticRetryCount { get; set; }

    public int AutomaticRetryLimit { get; set; }

    public DateTimeOffset? NextRetryAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MeetingTranscript? Transcript { get; set; }

    public MeetingMinutes? Minutes { get; set; }

    public void ValidateModeInput()
    {
        if (string.IsNullOrWhiteSpace(OriginalFileName))
        {
            throw new InvalidOperationException("A source file name is required.");
        }

        if (string.IsNullOrWhiteSpace(OriginalFilePath))
        {
            throw new InvalidOperationException("A source file path is required.");
        }

        if (!Enum.IsDefined(ProcessingMode))
        {
            throw new InvalidOperationException("The processing mode is invalid.");
        }

        if (SourceAudioDurationSeconds < 0)
        {
            throw new InvalidOperationException("Source audio duration cannot be negative.");
        }

        if (!ProcessingMode.RequiresAudio() &&
            (!string.IsNullOrWhiteSpace(ProcessedFilePath) || SourceAudioDurationSeconds is not null))
        {
            throw new InvalidOperationException(
                "Transcript-input jobs cannot contain processed-audio metadata.");
        }
    }
}

using MeetingMind.Domain.Enums;

namespace MeetingMind.Domain.Entities;

public class MeetingJob
{
    public Guid Id { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string OriginalFilePath { get; set; } = string.Empty;

    public string? ProcessedFilePath { get; set; }

    public MeetingJobStatus Status { get; set; } = MeetingJobStatus.Queued;

    public MeetingJobStage Stage { get; set; } = MeetingJobStage.Uploaded;

    public int Progress { get; set; }

    public string? ErrorMessage { get; set; }

    public string? HangfireJobId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MeetingTranscript? Transcript { get; set; }

    public MeetingMinutes? Minutes { get; set; }
}

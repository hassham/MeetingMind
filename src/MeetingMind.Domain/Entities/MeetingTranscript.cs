namespace MeetingMind.Domain.Entities;

public class MeetingTranscript
{
    public Guid Id { get; set; }

    public Guid MeetingJobId { get; set; }

    public string TranscriptText { get; set; } = string.Empty;

    public string? TranscriptFilePath { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MeetingJob? MeetingJob { get; set; }
}

namespace MeetingMind.Domain.Entities;

public class MeetingMinutes
{
    public Guid Id { get; set; }

    public Guid MeetingJobId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string DecisionsJson { get; set; } = "[]";

    public string ActionItemsJson { get; set; } = "[]";

    public string RisksJson { get; set; } = "[]";

    public string NextStepsJson { get; set; } = "[]";

    public string FullMinutesJson { get; set; } = "{}";

    public string? MinutesFilePath { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MeetingJob? MeetingJob { get; set; }
}

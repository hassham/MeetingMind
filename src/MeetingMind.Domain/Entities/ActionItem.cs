using MeetingMind.Domain.Enums;

namespace MeetingMind.Domain.Entities;

public sealed class ActionItem
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Assignee { get; set; }
    public string? Notes { get; set; }
    public DateOnly? DueDate { get; set; }
    public ActionItemStatus Status { get; set; } = ActionItemStatus.Open;
    public ActionItemSource Source { get; set; } = ActionItemSource.Manual;
    public Guid? MeetingJobId { get; set; }
    public string? ProvenanceMeetingTitle { get; set; }
    public string? ProvenanceSourceFileName { get; set; }
    public string? GeneratedSourceKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public long Version { get; set; } = 1;
    public MeetingJob? MeetingJob { get; set; }
}

using MeetingMind.Domain.Enums;

namespace MeetingMind.Application.Actions;

public sealed record ActionItemView(
    Guid Id, string Description, string? Assignee, string? Notes, DateOnly? DueDate,
    ActionItemStatus Status, ActionItemSource Source, Guid? MeetingId,
    string? MeetingTitle, string? SourceFileName, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? CompletedAt, string Version,
    bool IsOverdue);

public sealed record ActionListResult(IReadOnlyList<ActionItemView> Items, int Skip, int Take, int Total);

public sealed record ActionQuery(
    int Skip = 0, int Take = 25, ActionItemStatus? Status = null,
    string? Assignee = null, string? Due = null, ActionItemSource? Source = null,
    Guid? MeetingId = null);

public sealed record CreateActionRequest(
    string Description, string? Assignee, string? Notes, DateOnly? DueDate, Guid? MeetingId);

public sealed record UpdateActionRequest(
    string Description, string? Assignee, string? Notes, DateOnly? DueDate,
    ActionItemStatus Status, Guid? MeetingId, string Version);

public sealed record ActionExportRequest(
    string Format, IReadOnlyList<Guid>? Ids, ActionItemStatus? Status,
    string? Assignee, string? Due, ActionItemSource? Source, Guid? MeetingId);

public sealed record ActionExportFile(byte[] Content, string ContentType, string FileName);

public sealed record ActionBackfillResult(int ProcessedMeetings, int CreatedActions, bool HasMore);

public sealed class ActionValidationException : Exception
{
    public ActionValidationException(string message) : base(message) { }
}

public sealed class ActionNotFoundException : Exception;
public sealed class ActionConflictException : Exception;

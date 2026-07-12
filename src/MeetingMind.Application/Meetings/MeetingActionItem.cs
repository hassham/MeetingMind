namespace MeetingMind.Application.Meetings;

public sealed record MeetingActionItem(
    string Description,
    string? Owner,
    string? DueDate);

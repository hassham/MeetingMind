namespace MeetingMind.Application.Meetings;

public sealed record MeetingMinutesContent(
    string Title,
    string Summary,
    IReadOnlyList<string> Attendees,
    IReadOnlyList<string> DiscussionPoints,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<MeetingActionItem> ActionItems,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> NextSteps);

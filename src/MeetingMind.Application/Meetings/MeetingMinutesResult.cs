namespace MeetingMind.Application.Meetings;

public sealed record MeetingMinutesResult(
    Guid JobId,
    string OriginalFileName,
    string SourceType,
    string ProcessingMode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    bool HasTranscript,
    string Title,
    string Summary,
    IReadOnlyList<string> Attendees,
    IReadOnlyList<string> DiscussionPoints,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<MeetingActionItem> ActionItems,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> NextSteps);

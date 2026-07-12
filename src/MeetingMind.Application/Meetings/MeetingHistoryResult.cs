namespace MeetingMind.Application.Meetings;

public sealed record MeetingHistoryResult(
    int Skip,
    int Take,
    int Total,
    IReadOnlyList<MeetingHistoryItem> Items);

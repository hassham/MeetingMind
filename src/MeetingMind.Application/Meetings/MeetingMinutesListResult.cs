namespace MeetingMind.Application.Meetings;

public sealed record MeetingMinutesListResult(
    int Skip,
    int Take,
    int Total,
    IReadOnlyList<MeetingMinutesListItem> Items);

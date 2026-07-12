namespace MeetingMind.Application.Meetings;

public sealed record MeetingMinutesDownloadResult(
    Stream Content,
    string ContentType,
    string FileName);

namespace MeetingMind.Application.Meetings;

public sealed record UploadMeetingRequest(
    Stream File,
    string FileName,
    string ContentType,
    long Length);

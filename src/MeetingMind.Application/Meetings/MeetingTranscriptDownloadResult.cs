namespace MeetingMind.Application.Meetings;

public record MeetingTranscriptDownloadResult(
    Stream Content,
    string ContentType,
    string FileName);

namespace MeetingMind.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveOriginalAudioAsync(Stream file, string originalFileName, CancellationToken cancellationToken);

    Task<string> SaveTranscriptAsync(Guid meetingJobId, string transcriptText, CancellationToken cancellationToken);

    Task<string> SaveMinutesAsync(Guid meetingJobId, string minutesMarkdown, CancellationToken cancellationToken);

    Task<Stream> ReadAsync(string filePath, CancellationToken cancellationToken);

    Task DeleteAsync(string filePath, CancellationToken cancellationToken);
}

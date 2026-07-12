using MeetingMind.Application.Common.Interfaces;

namespace MeetingMind.Application.Meetings;

public class MeetingTranscriptService : IMeetingTranscriptService
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IMeetingJobRepository _meetingJobRepository;

    public MeetingTranscriptService(
        IFileStorageService fileStorageService,
        IMeetingJobRepository meetingJobRepository)
    {
        _fileStorageService = fileStorageService;
        _meetingJobRepository = meetingJobRepository;
    }

    public async Task<MeetingTranscriptDownloadResult?> GetTranscriptDownloadAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var transcript = await _meetingJobRepository.GetTranscriptByJobIdAsync(jobId, cancellationToken);
        if (transcript?.TranscriptFilePath is null)
        {
            return null;
        }

        var stream = await _fileStorageService.ReadAsync(transcript.TranscriptFilePath, cancellationToken);
        return new MeetingTranscriptDownloadResult(
            stream,
            "text/plain",
            $"meeting-transcript-{jobId:N}.txt");
    }
}

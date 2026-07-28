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

    public async Task<MeetingTranscriptResult?> GetTranscriptAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var transcript = await _meetingJobRepository.GetTranscriptByJobIdAsync(jobId, cancellationToken);
        if (transcript is null)
        {
            return null;
        }

        var structured = await _meetingJobRepository.GetStructuredTranscriptCheckpointAsync(
            jobId,
            cancellationToken);
        if (structured is not null)
        {
            return new MeetingTranscriptResult(
                jobId,
                HasTimestamps: true,
                structured.Formatting.FormattingVersion,
                structured.Paragraphs
                    .Select(paragraph => new MeetingTranscriptParagraphResult(
                        paragraph.Text,
                        paragraph.Start?.TotalSeconds,
                        paragraph.End?.TotalSeconds))
                    .ToArray());
        }

        var paragraphs = transcript.TranscriptText
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(text => new MeetingTranscriptParagraphResult(text, null, null))
            .ToArray();

        return new MeetingTranscriptResult(
            jobId,
            HasTimestamps: false,
            FormattingVersion: null,
            paragraphs);
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

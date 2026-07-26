using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using System.Text;

namespace MeetingMind.Application.Meetings;

public class UploadMeetingService : IUploadMeetingService
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedMimeTypesByExtension =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".mp3"] = ["audio/mpeg", "audio/mp3"],
            [".wav"] = ["audio/wav", "audio/x-wav", "audio/wave", "audio/vnd.wave"],
            [".m4a"] = ["audio/mp4", "audio/x-m4a", "audio/m4a"],
            [".aac"] = ["audio/aac", "audio/aacp", "audio/x-aac"]
        };
    private static readonly IReadOnlyDictionary<string, string[]> AllowedTranscriptMimeTypesByExtension =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".txt"] = ["text/plain"],
            [".md"] = ["text/markdown"]
        };
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly IFileStorageService _fileStorageService;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IMeetingJobRepository _meetingJobRepository;
    private readonly StorageOptions _storageOptions;

    public UploadMeetingService(
        IFileStorageService fileStorageService,
        IBackgroundJobService backgroundJobService,
        IMeetingJobRepository meetingJobRepository,
        StorageOptions storageOptions)
    {
        _fileStorageService = fileStorageService;
        _backgroundJobService = backgroundJobService;
        _meetingJobRepository = meetingJobRepository;
        _storageOptions = storageOptions;
    }

    public async Task<UploadMeetingResult> UploadAsync(
        UploadMeetingRequest request,
        CancellationToken cancellationToken)
    {
        ValidateAudioUpload(request);

        var originalFilePath = await _fileStorageService.SaveOriginalAudioAsync(
            request.File,
            request.FileName,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var meetingJob = new MeetingJob
        {
            Id = Guid.NewGuid(),
            OriginalFileName = Path.GetFileName(request.FileName),
            OriginalFilePath = originalFilePath,
            ProcessingMode = request.ProcessingMode,
            Status = MeetingJobStatus.Queued,
            Stage = MeetingJobStage.Uploaded,
            Progress = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _meetingJobRepository.AddAsync(meetingJob, cancellationToken);

        var hangfireJobId = _backgroundJobService.EnqueueMeetingProcessing(meetingJob.Id);
        await _meetingJobRepository.SetHangfireJobIdAsync(meetingJob.Id, hangfireJobId, cancellationToken);

        return new UploadMeetingResult(
            meetingJob.Id,
            meetingJob.ProcessingMode,
            meetingJob.Status,
            meetingJob.Stage);
    }

    public async Task<UploadMeetingResult> UploadTranscriptAsync(
        UploadMeetingRequest request,
        CancellationToken cancellationToken)
    {
        ValidateTranscriptMetadata(request);
        var transcriptText = await ReadAndValidateTranscriptAsync(request, cancellationToken);
        var jobId = Guid.NewGuid();
        string? transcriptPath = null;
        var jobPersisted = false;

        try
        {
            transcriptPath = await _fileStorageService.SaveTranscriptAsync(
                jobId,
                transcriptText,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var meetingJob = new MeetingJob
            {
                Id = jobId,
                OriginalFileName = Path.GetFileName(request.FileName),
                OriginalFilePath = transcriptPath,
                ProcessingMode = MeetingProcessingMode.MinutesFromTranscript,
                Status = MeetingJobStatus.Queued,
                Stage = MeetingJobStage.Uploaded,
                Progress = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _meetingJobRepository.AddAsync(meetingJob, cancellationToken);
            jobPersisted = true;
            await _meetingJobRepository.SaveTranscriptAsync(
                jobId,
                transcriptText,
                transcriptPath,
                cancellationToken);

            var hangfireJobId = _backgroundJobService.EnqueueMeetingProcessing(jobId);
            await _meetingJobRepository.SetHangfireJobIdAsync(jobId, hangfireJobId, cancellationToken);

            return new UploadMeetingResult(
                jobId,
                meetingJob.ProcessingMode,
                meetingJob.Status,
                meetingJob.Stage);
        }
        catch
        {
            if (transcriptPath is not null && !jobPersisted)
            {
                await _fileStorageService.DeleteAsync(transcriptPath, CancellationToken.None);
            }

            throw;
        }
    }

    private void ValidateAudioUpload(UploadMeetingRequest request)
    {
        if (request.ProcessingMode is not
            (MeetingProcessingMode.TranscriptOnly or MeetingProcessingMode.FullMeeting))
        {
            throw new UploadValidationException(
                "Audio processing mode must be TranscriptOnly or FullMeeting.");
        }

        if (request.File is null || !request.File.CanRead)
        {
            throw new UploadValidationException("A readable audio file is required.");
        }

        if (request.Length <= 0)
        {
            throw new UploadValidationException("Uploaded file is empty.");
        }

        if (request.Length > _storageOptions.MaxUploadSizeBytes)
        {
            throw new UploadValidationException($"Uploaded file exceeds the {_storageOptions.MaxUploadSizeMb} MB limit.");
        }

        var fileName = Path.GetFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(fileName) || !string.Equals(fileName, request.FileName, StringComparison.Ordinal))
        {
            throw new UploadValidationException("Invalid file name.");
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new UploadValidationException("File name contains invalid characters.");
        }

        var extension = Path.GetExtension(fileName);
        if (!_storageOptions.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new UploadValidationException("Unsupported file extension.");
        }

        if (string.IsNullOrWhiteSpace(request.ContentType)
            || !AllowedMimeTypesByExtension.TryGetValue(extension, out var allowedMimeTypes)
            || !allowedMimeTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new UploadValidationException("Unsupported file MIME type.");
        }
    }

    private void ValidateTranscriptMetadata(UploadMeetingRequest request)
    {
        if (request.File is null || !request.File.CanRead)
        {
            throw new UploadValidationException("A readable transcript file is required.");
        }

        if (request.Length <= 0)
        {
            throw new UploadValidationException("Uploaded transcript is empty.");
        }

        if (request.Length > _storageOptions.MaxTranscriptUploadSizeBytes)
        {
            throw new UploadValidationException(
                $"Uploaded transcript exceeds the {_storageOptions.MaxTranscriptUploadSizeMb} MB limit.");
        }

        var fileName = Path.GetFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, request.FileName, StringComparison.Ordinal) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new UploadValidationException("Invalid transcript file name.");
        }

        var extension = Path.GetExtension(fileName);
        if (!AllowedTranscriptMimeTypesByExtension.TryGetValue(extension, out var allowedMimeTypes))
        {
            throw new UploadValidationException("Transcript must be a .txt or .md file.");
        }

        if (string.IsNullOrWhiteSpace(request.ContentType) ||
            !allowedMimeTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new UploadValidationException(
                $"Unsupported MIME type for {extension.ToLowerInvariant()} transcript.");
        }
    }

    private async Task<string> ReadAndValidateTranscriptAsync(
        UploadMeetingRequest request,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(capacity: (int)Math.Min(request.Length, int.MaxValue));
        await request.File.CopyToAsync(buffer, cancellationToken);

        string transcriptText;
        try
        {
            transcriptText = StrictUtf8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
        }
        catch (DecoderFallbackException)
        {
            throw new UploadValidationException("Transcript must contain valid UTF-8 text.");
        }

        if (transcriptText.Length > 0 && transcriptText[0] == '\uFEFF')
        {
            transcriptText = transcriptText[1..];
        }

        transcriptText = transcriptText.Replace("\r\n", "\n").Replace('\r', '\n');

        if (transcriptText.Any(character =>
                character == '\0' ||
                (char.IsControl(character) && character is not ('\n' or '\t'))))
        {
            throw new UploadValidationException("Binary transcript content is not supported.");
        }

        if (string.IsNullOrWhiteSpace(transcriptText))
        {
            throw new UploadValidationException("Transcript must contain non-whitespace text.");
        }

        if (transcriptText.Length > _storageOptions.MaxTranscriptCharacters)
        {
            throw new UploadValidationException(
                $"Transcript exceeds the {_storageOptions.MaxTranscriptCharacters} character limit.");
        }

        return transcriptText;
    }
}

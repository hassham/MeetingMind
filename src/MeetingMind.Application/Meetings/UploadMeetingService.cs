using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

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
        ValidateUpload(request);

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
            Status = MeetingJobStatus.Queued,
            Stage = MeetingJobStage.Uploaded,
            Progress = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _meetingJobRepository.AddAsync(meetingJob, cancellationToken);

        var hangfireJobId = _backgroundJobService.EnqueueMeetingProcessing(meetingJob.Id);
        await _meetingJobRepository.SetHangfireJobIdAsync(meetingJob.Id, hangfireJobId, cancellationToken);

        return new UploadMeetingResult(meetingJob.Id, meetingJob.Status, meetingJob.Stage);
    }

    private void ValidateUpload(UploadMeetingRequest request)
    {
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
}

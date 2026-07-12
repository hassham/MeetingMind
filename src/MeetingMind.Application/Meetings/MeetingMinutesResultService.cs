using System.Text.Json;
using MeetingMind.Application.Common.Interfaces;

namespace MeetingMind.Application.Meetings;

public class MeetingMinutesResultService : IMeetingMinutesResultService
{
    private const string MarkdownContentType = "text/markdown";

    private readonly IMeetingJobRepository _meetingJobRepository;
    private readonly IFileStorageService _fileStorageService;

    public MeetingMinutesResultService(
        IMeetingJobRepository meetingJobRepository,
        IFileStorageService fileStorageService)
    {
        _meetingJobRepository = meetingJobRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<MeetingMinutesResult?> GetMinutesAsync(
        Guid meetingJobId,
        CancellationToken cancellationToken)
    {
        var minutes = await _meetingJobRepository.GetMinutesByJobIdAsync(meetingJobId, cancellationToken);
        if (minutes is null)
        {
            return null;
        }

        var fullMinutes = JsonSerializer.Deserialize<MeetingMinutesContent>(
            minutes.FullMinutesJson,
            JsonOptions()) ?? EmptyContent(minutes.Title, minutes.Summary);

        return new MeetingMinutesResult(
            meetingJobId,
            minutes.Title,
            minutes.Summary,
            fullMinutes.Attendees,
            fullMinutes.DiscussionPoints,
            DeserializeStringList(minutes.DecisionsJson),
            DeserializeActionItems(minutes.ActionItemsJson),
            DeserializeStringList(minutes.RisksJson),
            DeserializeStringList(minutes.NextStepsJson));
    }

    public async Task<MeetingMinutesDownloadResult?> GetMinutesDownloadAsync(
        Guid meetingJobId,
        CancellationToken cancellationToken)
    {
        var minutes = await _meetingJobRepository.GetMinutesByJobIdAsync(meetingJobId, cancellationToken);
        if (minutes?.MinutesFilePath is null)
        {
            return null;
        }

        var stream = await _fileStorageService.ReadAsync(minutes.MinutesFilePath, cancellationToken);
        return new MeetingMinutesDownloadResult(
            stream,
            MarkdownContentType,
            $"meeting-{meetingJobId:N}-minutes.md");
    }

    private static MeetingMinutesContent EmptyContent(string title, string summary)
    {
        return new MeetingMinutesContent(
            title,
            summary,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<MeetingActionItem>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static IReadOnlyList<string> DeserializeStringList(string json)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonOptions())
            ?? Array.Empty<string>();
    }

    private static IReadOnlyList<MeetingActionItem> DeserializeActionItems(string json)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<MeetingActionItem>>(json, JsonOptions())
            ?? Array.Empty<MeetingActionItem>();
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }
}

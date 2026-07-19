using MeetingMind.Application.Common.Interfaces;
using System.Collections.Concurrent;
using System.Text;

namespace MeetingMind.Api.IntegrationTests;

public sealed class TestBackgroundJobService : IBackgroundJobService
{
    private int _sequence;

    public List<Guid> EnqueuedJobIds { get; } = [];

    public string EnqueueMeetingProcessing(Guid jobId)
    {
        EnqueuedJobIds.Add(jobId);
        return $"test-hangfire-{Interlocked.Increment(ref _sequence)}";
    }

    public void Reset()
    {
        EnqueuedJobIds.Clear();
        _sequence = 0;
    }
}

public sealed class TestFileStorageService : IFileStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public Task<string> SaveOriginalAudioAsync(
        Stream file,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        var path = $"Audio/Original/{Guid.NewGuid():N}{Path.GetExtension(originalFileName)}";
        return SaveAsync(path, file, cancellationToken);
    }

    public Task<string> SaveTranscriptAsync(
        Guid meetingJobId,
        string transcriptText,
        CancellationToken cancellationToken)
    {
        var path = $"Transcript/{meetingJobId:N}.txt";
        _files[path] = Encoding.UTF8.GetBytes(transcriptText);
        return Task.FromResult(path);
    }

    public Task<string> SaveMinutesAsync(
        Guid meetingJobId,
        string minutesMarkdown,
        CancellationToken cancellationToken)
    {
        var path = $"Minutes/{meetingJobId:N}.md";
        _files[path] = Encoding.UTF8.GetBytes(minutesMarkdown);
        return Task.FromResult(path);
    }

    public Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.FromResult(_files.ContainsKey(filePath));
    }

    public Task<Stream> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!_files.TryGetValue(filePath, out var content))
        {
            throw new FileNotFoundException("Test artifact was not registered.", filePath);
        }

        return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
    }

    public Task DeleteAsync(string filePath, CancellationToken cancellationToken)
    {
        _files.TryRemove(filePath, out _);
        return Task.CompletedTask;
    }

    public void EnsurePathIsSafe(string filePath)
    {
    }

    public void AddText(string path, string content)
    {
        _files[path] = Encoding.UTF8.GetBytes(content);
    }

    public void Reset()
    {
        _files.Clear();
    }

    private async Task<string> SaveAsync(string path, Stream source, CancellationToken cancellationToken)
    {
        await using var target = new MemoryStream();
        await source.CopyToAsync(target, cancellationToken);
        _files[path] = target.ToArray();
        return path;
    }
}

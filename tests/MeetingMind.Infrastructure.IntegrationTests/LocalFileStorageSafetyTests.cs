using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Options;
using MeetingMind.Infrastructure.Storage;

namespace MeetingMind.Infrastructure.IntegrationTests;

public sealed class LocalFileStorageSafetyTests : IDisposable
{
    private readonly string _parentPath = Path.Combine(
        Path.GetTempPath(),
        "MeetingMind.StorageSafetyTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CleanupCannotDeleteFileOutsideConfiguredRoot()
    {
        var rootPath = Path.Combine(_parentPath, "storage");
        Directory.CreateDirectory(rootPath);
        var outsidePath = Path.Combine(_parentPath, "outside.txt");
        await File.WriteAllTextAsync(outsidePath, "must remain");
        var service = new LocalFileStorageService(new StorageOptions { RootPath = rootPath });

        await Assert.ThrowsAsync<PermanentMeetingProcessingException>(() =>
            service.DeleteAsync("../outside.txt", CancellationToken.None));

        Assert.True(File.Exists(outsidePath));
    }

    [Fact]
    public async Task TranscriptStorageWritesNormalizedContentWithoutUtf8Bom()
    {
        var rootPath = Path.Combine(_parentPath, "storage");
        var service = new LocalFileStorageService(new StorageOptions { RootPath = rootPath });
        var transcript = "First paragraph\n\nSecond paragraph";

        var path = await service.SaveTranscriptAsync(
            Guid.NewGuid(),
            transcript,
            CancellationToken.None);
        await using var stored = await service.ReadAsync(path, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stored.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Equal(transcript, System.Text.Encoding.UTF8.GetString(bytes));
    }

    public void Dispose()
    {
        if (Directory.Exists(_parentPath))
        {
            Directory.Delete(_parentPath, recursive: true);
        }
    }
}

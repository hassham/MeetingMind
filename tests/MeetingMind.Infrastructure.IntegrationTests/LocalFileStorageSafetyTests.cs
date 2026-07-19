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

    public void Dispose()
    {
        if (Directory.Exists(_parentPath))
        {
            Directory.Delete(_parentPath, recursive: true);
        }
    }
}

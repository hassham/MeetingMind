using MeetingMind.Application.Common.Options;
using MeetingMind.Infrastructure.Operations;
using MeetingMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MeetingMind.Infrastructure.IntegrationTests;

public sealed class OperationalReadinessServiceTests : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly PostgreSqlFixture _fixture;
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "MeetingMind.ReadinessTests",
        Guid.NewGuid().ToString("N"));

    public OperationalReadinessServiceTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task HealthyDependenciesAreReportedIndividually()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var service = CreateService(dbContext);

        var checks = await service.CheckAsync(CancellationToken.None);

        Assert.Equal(4, checks.Count);
        Assert.All(checks, check => Assert.True(check.IsHealthy, check.Name));
    }

    [Fact]
    public async Task MissingFfmpegIsReportedWithoutHidingOtherChecks()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var service = CreateService(
            dbContext,
            audioOptions: new AudioProcessingOptions
            {
                FfmpegBinaryFolder = Path.Combine(_rootPath, "missing", "ffmpeg.exe")
            });

        var checks = await service.CheckAsync(CancellationToken.None);

        Assert.False(checks.Single(check => check.Name == "ffmpeg").IsHealthy);
        Assert.True(checks.Single(check => check.Name == "database").IsHealthy);
    }

    [Fact]
    public async Task MissingWhisperModelIsReportedUnhealthy()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var service = CreateService(
            dbContext,
            transcriptionOptions: new TranscriptionOptions
            {
                ModelPath = Path.Combine(_rootPath, "missing-model.bin"),
                ModelSize = "small"
            });

        var checks = await service.CheckAsync(CancellationToken.None);

        Assert.False(checks.Single(check => check.Name == "whisper_model").IsHealthy);
    }

    [Fact]
    public async Task UnwritableStorageTargetIsReportedUnhealthy()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var fileAsRoot = Path.Combine(_rootPath, "not-a-directory");
        await File.WriteAllTextAsync(fileAsRoot, "occupied");
        var service = CreateService(
            dbContext,
            storageOptions: new StorageOptions { RootPath = fileAsRoot });

        var checks = await service.CheckAsync(CancellationToken.None);

        Assert.False(checks.Single(check => check.Name == "storage").IsHealthy);
    }

    [Fact]
    public async Task UnavailableDatabaseIsReportedUnhealthy()
    {
        var options = new DbContextOptionsBuilder<MeetingMindDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1")
            .Options;
        await using var dbContext = new MeetingMindDbContext(options);
        var service = CreateService(dbContext);

        var checks = await service.CheckAsync(CancellationToken.None);

        Assert.False(checks.Single(check => check.Name == "database").IsHealthy);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private OperationalReadinessService CreateService(
        MeetingMindDbContext dbContext,
        StorageOptions? storageOptions = null,
        AudioProcessingOptions? audioOptions = null,
        TranscriptionOptions? transcriptionOptions = null)
    {
        storageOptions ??= new StorageOptions { RootPath = _rootPath };

        if (audioOptions is null)
        {
            var ffmpegPath = Path.Combine(_rootPath, "ffmpeg.exe");
            File.WriteAllBytes(ffmpegPath, [0]);
            audioOptions = new AudioProcessingOptions { FfmpegBinaryFolder = ffmpegPath };
        }

        if (transcriptionOptions is null)
        {
            var modelPath = Path.Combine(_rootPath, "whisper-model.bin");
            File.WriteAllBytes(modelPath, [0]);
            transcriptionOptions = new TranscriptionOptions
            {
                ModelPath = modelPath,
                ModelSize = "small"
            };
        }

        return new OperationalReadinessService(
            dbContext,
            storageOptions,
            audioOptions,
            transcriptionOptions);
    }
}

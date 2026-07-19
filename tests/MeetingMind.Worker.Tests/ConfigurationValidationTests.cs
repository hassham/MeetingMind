using MeetingMind.Application.Common.Options;
using MeetingMind.Infrastructure.Configuration;
using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Worker;
using Microsoft.Extensions.Configuration;

namespace MeetingMind.Worker.Tests;

public sealed class ConfigurationValidationTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "MeetingMind.ConfigurationTests",
        Guid.NewGuid().ToString("N"));

    public ConfigurationValidationTests()
    {
        Directory.CreateDirectory(_temporaryRoot);
    }

    [Fact]
    public void ValidSettingsAreAcceptedAndSharedStorageRootIsNormalizedIdentically()
    {
        var ffmpegPath = CreateFfmpegExecutable();
        var firstStorage = MeetingMindConfiguration.ValidateStorageOptions(CreateStorageOptions());
        var secondStorage = MeetingMindConfiguration.ValidateStorageOptions(CreateStorageOptions());

        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] =
            "Host=localhost;Database=meetingmind;Username=meetingmind_user;Password=meetingmind_password";

        var connectionString = MeetingMindConfiguration.GetConnectionString(configuration);
        var audio = MeetingMindConfiguration.ValidateAudioProcessingOptions(new AudioProcessingOptions
        {
            FfmpegBinaryFolder = ffmpegPath
        });
        var transcription = MeetingMindConfiguration.ValidateTranscriptionOptions(
            new TranscriptionOptions(),
            firstStorage);
        var openAi = MeetingMindConfiguration.ValidateOpenAiOptions(new OpenAiOptions
        {
            ApiKey = "test-key"
        });
        var automaticRetry = MeetingMindConfiguration.ValidateAutomaticRetryOptions(
            new AutomaticRetryOptions { DelaysInSeconds = [10, 60] });
        var minutesGeneration = MeetingMindConfiguration.ValidateMeetingMinutesGenerationOptions(
            new MeetingMinutesGenerationOptions());

        Assert.Equal(Path.GetFullPath(_temporaryRoot), firstStorage.RootPath);
        Assert.Equal(firstStorage.RootPath, secondStorage.RootPath);
        Assert.Equal(ffmpegPath, audio.FfmpegBinaryFolder);
        Assert.True(transcription.AutoDownloadModel);
        Assert.Equal("small", transcription.ModelSize);
        Assert.Equal("test-key", openAi.ApiKey);
        Assert.Equal(2, automaticRetry.RetryLimit);
        Assert.Equal([10, 60], automaticRetry.DelaysInSeconds);
        Assert.Equal(120000, minutesGeneration.SinglePassMaxCharacters);
        Assert.Equal(60000, minutesGeneration.ChunkSizeCharacters);
        Assert.Equal(1500, minutesGeneration.ChunkOverlapCharacters);
        Assert.Equal(1200000, minutesGeneration.MaxTranscriptCharacters);
        Assert.Equal(120000, minutesGeneration.MaxAggregationInputCharacters);
        Assert.Contains("Database=meetingmind", connectionString);
    }

    [Fact]
    public void MissingConnectionStringNamesTheSetting()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MeetingMindConfiguration.GetConnectionString(new ConfigurationManager()));

        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message);
    }

    [Fact]
    public void RelativeStorageRootNamesTheSetting()
    {
        var options = CreateStorageOptions();
        options.RootPath = "Storage";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MeetingMindConfiguration.ValidateStorageOptions(options));

        Assert.Contains("Storage:RootPath", exception.Message);
    }

    [Fact]
    public void InvalidUploadLimitNamesTheSetting()
    {
        var options = CreateStorageOptions();
        options.MaxUploadSizeMb = 0;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MeetingMindConfiguration.ValidateStorageOptions(options));

        Assert.Contains("Storage:MaxUploadSizeMb", exception.Message);
    }

    [Fact]
    public void MissingFfmpegPathNamesTheSetting()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MeetingMindConfiguration.ValidateAudioProcessingOptions(new AudioProcessingOptions()));

        Assert.Contains("AudioProcessing:FfmpegBinaryFolder", exception.Message);
    }

    [Fact]
    public void DisabledModelDownloadRequiresModelPath()
    {
        var options = new TranscriptionOptions
        {
            AutoDownloadModel = false,
            ModelPath = null
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MeetingMindConfiguration.ValidateTranscriptionOptions(options, CreateStorageOptions()));

        Assert.Contains("Transcription:ModelPath", exception.Message);
    }

    [Fact]
    public void MissingOpenAiKeyNamesTheSetting()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MeetingMindConfiguration.ValidateOpenAiOptions(new OpenAiOptions()));

        Assert.Contains("OpenAI:ApiKey", exception.Message);
    }

    [Fact]
    public void AutomaticRetryRequiresPositiveConfiguredDelays()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MeetingMindConfiguration.ValidateAutomaticRetryOptions(
                new AutomaticRetryOptions { DelaysInSeconds = [10, 0] }));

        Assert.Contains("AutomaticRetry:DelaysInSeconds:1", exception.Message);
    }

    [Fact]
    public void HangfireFilterUsesConfiguredAttemptsDelaysAndPermanentExclusion()
    {
        var filter = MeetingAutomaticRetryConfiguration.CreateFilter(
            new AutomaticRetryOptions { DelaysInSeconds = [10, 60] });

        Assert.Equal(2, filter.Attempts);
        Assert.Equal([10, 60], filter.DelaysInSeconds);
        Assert.Contains(typeof(PermanentMeetingProcessingException), filter.ExceptOn);
    }

    [Fact]
    public void MinutesGenerationRejectsOverlapAtOrAboveChunkSize()
    {
        var options = new MeetingMinutesGenerationOptions
        {
            ChunkSizeCharacters = 100,
            ChunkOverlapCharacters = 100
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MeetingMindConfiguration.ValidateMeetingMinutesGenerationOptions(options));

        Assert.Contains("MeetingMinutesGeneration:ChunkOverlapCharacters", exception.Message);
    }

    [Fact]
    public void MinutesGenerationRejectsMaximumBelowSinglePassLimit()
    {
        var options = new MeetingMinutesGenerationOptions
        {
            SinglePassMaxCharacters = 1000,
            ChunkSizeCharacters = 500,
            ChunkOverlapCharacters = 50,
            MaxTranscriptCharacters = 999
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MeetingMindConfiguration.ValidateMeetingMinutesGenerationOptions(options));

        Assert.Contains("MeetingMinutesGeneration:MaxTranscriptCharacters", exception.Message);
    }

    [Fact]
    public void StorageRetentionDefaultsAreSafeAndAccepted()
    {
        var options = MeetingMindConfiguration.ValidateStorageRetentionOptions(
            new StorageRetentionOptions());

        Assert.False(options.Enabled);
        Assert.Equal(30, options.RetentionDays);
        Assert.Equal("0 2 * * *", options.Schedule);
        Assert.Equal(100, options.BatchSize);
    }

    [Theory]
    [InlineData(0, 100, "StorageRetention:RetentionDays")]
    [InlineData(30, 0, "StorageRetention:BatchSize")]
    [InlineData(30, 1001, "StorageRetention:BatchSize")]
    public void StorageRetentionRejectsUnsafeBounds(
        int retentionDays,
        int batchSize,
        string expectedSetting)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MeetingMindConfiguration.ValidateStorageRetentionOptions(
                new StorageRetentionOptions
                {
                    RetentionDays = retentionDays,
                    BatchSize = batchSize
                }));

        Assert.Contains(expectedSetting, exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private StorageOptions CreateStorageOptions()
    {
        return new StorageOptions
        {
            RootPath = _temporaryRoot
        };
    }

    private string CreateFfmpegExecutable()
    {
        var path = Path.Combine(_temporaryRoot, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        File.WriteAllText(path, string.Empty);
        return path;
    }
}

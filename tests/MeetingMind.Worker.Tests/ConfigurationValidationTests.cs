using MeetingMind.Application.Common.Options;
using MeetingMind.Infrastructure.Configuration;
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

        Assert.Equal(Path.GetFullPath(_temporaryRoot), firstStorage.RootPath);
        Assert.Equal(firstStorage.RootPath, secondStorage.RootPath);
        Assert.Equal(ffmpegPath, audio.FfmpegBinaryFolder);
        Assert.True(transcription.AutoDownloadModel);
        Assert.Equal("small", transcription.ModelSize);
        Assert.Equal("test-key", openAi.ApiKey);
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

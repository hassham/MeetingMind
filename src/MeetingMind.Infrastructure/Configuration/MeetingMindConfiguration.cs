using MeetingMind.Application.Common.Options;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MeetingMind.Infrastructure.Configuration;

public static class MeetingMindConfiguration
{
    private const string ConnectionStringName = "ConnectionStrings:DefaultConnection";

    private static readonly HashSet<string> SupportedWhisperModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "tiny",
        "base",
        "small",
        "medium",
        "large-v1",
        "large-v2",
        "large-v3",
        "large-v3-turbo"
    };

    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw Invalid(ConnectionStringName, "is required");
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            RequireText(builder.Host, ConnectionStringName, "must include Host");
            RequireText(builder.Database, ConnectionStringName, "must include Database");
            RequireText(builder.Username, ConnectionStringName, "must include Username");
        }
        catch (ArgumentException exception)
        {
            throw Invalid(ConnectionStringName, "is not a valid PostgreSQL connection string", exception);
        }

        return connectionString;
    }

    public static StorageOptions ValidateStorageOptions(StorageOptions options)
    {
        RequireText(options.RootPath, "Storage:RootPath", "is required");

        if (!Path.IsPathFullyQualified(options.RootPath))
        {
            throw Invalid("Storage:RootPath", "must be an absolute path");
        }

        options.RootPath = Path.GetFullPath(options.RootPath);

        if (options.MaxUploadSizeMb <= 0)
        {
            throw Invalid("Storage:MaxUploadSizeMb", "must be greater than zero");
        }

        if (options.AllowedExtensions is null || options.AllowedExtensions.Length == 0)
        {
            throw Invalid("Storage:AllowedExtensions", "must contain at least one extension");
        }

        for (var index = 0; index < options.AllowedExtensions.Length; index++)
        {
            var extension = options.AllowedExtensions[index];
            if (string.IsNullOrWhiteSpace(extension) || !extension.StartsWith('.') || extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw Invalid($"Storage:AllowedExtensions:{index}", "must be a valid extension beginning with '.'");
            }

            options.AllowedExtensions[index] = extension.ToLowerInvariant();
        }

        ValidateStorageFolder(options.RootPath, options.OriginalAudioFolder, "Storage:OriginalAudioFolder");
        ValidateStorageFolder(options.RootPath, options.ProcessedAudioFolder, "Storage:ProcessedAudioFolder");
        ValidateStorageFolder(options.RootPath, options.TranscriptFolder, "Storage:TranscriptFolder");
        ValidateStorageFolder(options.RootPath, options.MinutesFolder, "Storage:MinutesFolder");

        return options;
    }

    public static AudioProcessingOptions ValidateAudioProcessingOptions(AudioProcessingOptions options)
    {
        RequireText(options.FfmpegBinaryFolder, "AudioProcessing:FfmpegBinaryFolder", "is required");
        var ffmpegPath = options.FfmpegBinaryFolder!;
        if (!Path.IsPathFullyQualified(ffmpegPath))
        {
            throw Invalid("AudioProcessing:FfmpegBinaryFolder", "must be an absolute folder or executable path");
        }

        var configuredPath = Path.GetFullPath(ffmpegPath);
        if (File.Exists(configuredPath))
        {
            var fileName = Path.GetFileName(configuredPath);
            if (!fileName.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase) &&
                !fileName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            {
                throw Invalid("AudioProcessing:FfmpegBinaryFolder", "must point to an ffmpeg executable or its containing folder");
            }
        }
        else if (Directory.Exists(configuredPath))
        {
            if (!File.Exists(Path.Combine(configuredPath, "ffmpeg")) &&
                !File.Exists(Path.Combine(configuredPath, "ffmpeg.exe")))
            {
                throw Invalid("AudioProcessing:FfmpegBinaryFolder", "does not contain an ffmpeg executable");
            }
        }
        else
        {
            throw Invalid("AudioProcessing:FfmpegBinaryFolder", "does not exist");
        }

        options.FfmpegBinaryFolder = configuredPath;
        RequireText(options.OutputExtension, "AudioProcessing:OutputExtension", "is required");
        RequireText(options.OutputFormat, "AudioProcessing:OutputFormat", "is required");
        RequireText(options.AudioCodec, "AudioProcessing:AudioCodec", "is required");

        if (options.SampleRate <= 0)
        {
            throw Invalid("AudioProcessing:SampleRate", "must be greater than zero");
        }

        if (options.Channels <= 0)
        {
            throw Invalid("AudioProcessing:Channels", "must be greater than zero");
        }

        if (options.AudioBitrateKbps is <= 0)
        {
            throw Invalid("AudioProcessing:AudioBitrateKbps", "must be greater than zero when configured");
        }

        return options;
    }

    public static TranscriptionOptions ValidateTranscriptionOptions(
        TranscriptionOptions options,
        StorageOptions storageOptions)
    {
        RequireText(options.ModelSize, "Transcription:ModelSize", "is required");
        if (!SupportedWhisperModels.Contains(options.ModelSize))
        {
            throw Invalid("Transcription:ModelSize", "is not a supported Whisper model size");
        }

        options.ModelSize = options.ModelSize.Trim().ToLowerInvariant();
        RequireText(options.Language, "Transcription:Language", "is required");

        if (!string.IsNullOrWhiteSpace(options.ModelPath))
        {
            if (!Path.IsPathFullyQualified(options.ModelPath))
            {
                throw Invalid("Transcription:ModelPath", "must be an absolute path when configured");
            }

            options.ModelPath = Path.GetFullPath(options.ModelPath);
            if (!File.Exists(options.ModelPath))
            {
                throw Invalid("Transcription:ModelPath", "does not exist");
            }

            return options;
        }

        if (!options.AutoDownloadModel)
        {
            throw Invalid("Transcription:ModelPath", "is required when Transcription:AutoDownloadModel is false");
        }

        ValidateStorageFolder(storageOptions.RootPath, options.ModelDirectory, "Transcription:ModelDirectory");
        return options;
    }

    public static OpenAiOptions ValidateOpenAiOptions(OpenAiOptions options)
    {
        RequireText(options.ApiKey, "OpenAI:ApiKey", "is required");
        RequireText(options.Model, "OpenAI:Model", "is required");

        return options;
    }

    public static MeetingMinutesGenerationOptions ValidateMeetingMinutesGenerationOptions(
        MeetingMinutesGenerationOptions options)
    {
        RequirePositive(options.SinglePassMaxCharacters, "MeetingMinutesGeneration:SinglePassMaxCharacters");
        RequirePositive(options.ChunkSizeCharacters, "MeetingMinutesGeneration:ChunkSizeCharacters");
        RequirePositive(options.MaxTranscriptCharacters, "MeetingMinutesGeneration:MaxTranscriptCharacters");
        RequirePositive(
            options.MaxAggregationInputCharacters,
            "MeetingMinutesGeneration:MaxAggregationInputCharacters");

        if (options.ChunkOverlapCharacters < 0 ||
            options.ChunkOverlapCharacters >= options.ChunkSizeCharacters)
        {
            throw Invalid(
                "MeetingMinutesGeneration:ChunkOverlapCharacters",
                "must be non-negative and smaller than the chunk size");
        }

        if (options.ChunkSizeCharacters > options.SinglePassMaxCharacters)
        {
            throw Invalid(
                "MeetingMinutesGeneration:ChunkSizeCharacters",
                "must not exceed the single-pass limit");
        }

        if (options.MaxTranscriptCharacters < options.SinglePassMaxCharacters)
        {
            throw Invalid(
                "MeetingMinutesGeneration:MaxTranscriptCharacters",
                "must be greater than or equal to the single-pass limit");
        }

        return options;
    }

    public static AutomaticRetryOptions ValidateAutomaticRetryOptions(AutomaticRetryOptions options)
    {
        if (options.DelaysInSeconds is null || options.DelaysInSeconds.Length == 0)
        {
            throw Invalid("AutomaticRetry:DelaysInSeconds", "must contain at least one delay");
        }

        if (options.DelaysInSeconds.Length > 10)
        {
            throw Invalid("AutomaticRetry:DelaysInSeconds", "must contain no more than ten delays");
        }

        for (var index = 0; index < options.DelaysInSeconds.Length; index++)
        {
            if (options.DelaysInSeconds[index] <= 0)
            {
                throw Invalid($"AutomaticRetry:DelaysInSeconds:{index}", "must be greater than zero");
            }
        }

        return options;
    }

    public static StorageRetentionOptions ValidateStorageRetentionOptions(StorageRetentionOptions options)
    {
        RequirePositive(options.RetentionDays, "StorageRetention:RetentionDays");
        RequirePositive(options.BatchSize, "StorageRetention:BatchSize");
        RequireText(options.Schedule, "StorageRetention:Schedule", "is required");

        if (options.BatchSize > 1000)
        {
            throw Invalid("StorageRetention:BatchSize", "must not exceed 1000");
        }

        return options;
    }

    private static void ValidateStorageFolder(string rootPath, string folder, string settingName)
    {
        RequireText(folder, settingName, "is required");
        if (Path.IsPathRooted(folder))
        {
            throw Invalid(settingName, "must be relative to Storage:RootPath");
        }

        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, folder));
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid(settingName, "must remain inside Storage:RootPath");
        }
    }

    private static void RequireText(string? value, string settingName, string requirement)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(settingName, requirement);
        }
    }

    private static void RequirePositive(int value, string settingName)
    {
        if (value <= 0)
        {
            throw Invalid(settingName, "must be greater than zero");
        }
    }

    private static InvalidOperationException Invalid(string settingName, string message, Exception? innerException = null)
    {
        return new InvalidOperationException($"Configuration setting '{settingName}' {message}.", innerException);
    }
}

using FFMpegCore;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;

namespace MeetingMind.Infrastructure.Audio;

public class FfmpegAudioProcessingService : IAudioProcessingService
{
    private const int MaxErrorLength = 1000;

    private readonly AudioProcessingOptions _audioProcessingOptions;
    private readonly StorageOptions _storageOptions;

    public FfmpegAudioProcessingService(
        AudioProcessingOptions audioProcessingOptions,
        StorageOptions storageOptions)
    {
        _audioProcessingOptions = audioProcessingOptions;
        _storageOptions = storageOptions;
        var binaryFolder = GetConfiguredBinaryFolder();

        if (!string.IsNullOrWhiteSpace(binaryFolder))
        {
            GlobalFFOptions.Configure(options =>
            {
                options.BinaryFolder = binaryFolder;
            });
        }
    }

    public async Task<string> ConvertToStandardFormatAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        var inputFullPath = GetSafeFullPath(inputPath);
        if (!File.Exists(inputFullPath))
        {
            throw new FileNotFoundException("Uploaded audio file was not found.", inputPath);
        }

        var outputFileName = $"{Guid.NewGuid():N}{NormalizeExtension(_audioProcessingOptions.OutputExtension)}";
        var outputRelativePath = Path.Combine(_storageOptions.ProcessedAudioFolder, outputFileName);
        var outputFullPath = GetSafeFullPath(outputRelativePath);

        try
        {
            await FFMpegArguments
                .FromFileInput(inputFullPath)
                .OutputToFile(outputFullPath, overwrite: false, options => options
                    .WithCustomArgument("-vn")
                    .WithAudioCodec(_audioProcessingOptions.AudioCodec)
                    .WithAudioSamplingRate(_audioProcessingOptions.SampleRate)
                    .WithCustomArgument($"-ac {_audioProcessingOptions.Channels}")
                    .ForceFormat(_audioProcessingOptions.OutputFormat))
                .ProcessAsynchronously(true, new FFOptions
                {
                    BinaryFolder = GetConfiguredBinaryFolder()
                });
        }
        catch (Exception exception)
        {
            if (File.Exists(outputFullPath))
            {
                File.Delete(outputFullPath);
            }

            throw new InvalidOperationException(SanitizeError(exception.Message), exception);
        }

        return NormalizePath(outputRelativePath);
    }

    private string GetSafeFullPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Storage paths must be relative.");
        }

        var rootPath = Path.GetFullPath(_storageOptions.RootPath);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage path escapes the configured root.");
        }

        return fullPath;
    }

    private string GetConfiguredBinaryFolder()
    {
        if (string.IsNullOrWhiteSpace(_audioProcessingOptions.FfmpegBinaryFolder))
        {
            return string.Empty;
        }

        var configuredPath = _audioProcessingOptions.FfmpegBinaryFolder;
        var fileName = Path.GetFileName(configuredPath);

        if (fileName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(configuredPath) ?? string.Empty;
        }

        return configuredPath;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".mp3";
        }

        return extension.StartsWith('.')
            ? extension.ToLowerInvariant()
            : $".{extension.ToLowerInvariant()}";
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string SanitizeError(string message)
    {
        var sanitized = message
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        if (sanitized.Length <= MaxErrorLength)
        {
            return sanitized;
        }

        return sanitized[..MaxErrorLength];
    }
}

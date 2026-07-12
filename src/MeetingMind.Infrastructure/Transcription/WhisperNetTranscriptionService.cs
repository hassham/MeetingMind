using System.Text;
using FFMpegCore;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using Whisper.net;
using Whisper.net.Ggml;

namespace MeetingMind.Infrastructure.Transcription;

public class WhisperNetTranscriptionService : ITranscriptionService
{
    private const long MinimumModelSizeBytes = 1024 * 1024;

    private readonly StorageOptions _storageOptions;
    private readonly TranscriptionOptions _transcriptionOptions;

    public WhisperNetTranscriptionService(
        StorageOptions storageOptions,
        TranscriptionOptions transcriptionOptions)
    {
        _storageOptions = storageOptions;
        _transcriptionOptions = transcriptionOptions;
    }

    public async Task<string> TranscribeAsync(string audioPath, CancellationToken cancellationToken)
    {
        var audioFullPath = GetSafeStorageFullPath(audioPath);
        if (!File.Exists(audioFullPath))
        {
            throw new FileNotFoundException("Processed audio file was not found.", audioPath);
        }

        var modelPath = await GetModelPathAsync(cancellationToken);
        using var whisperFactory = WhisperFactory.FromPath(modelPath);
        using var processor = CreateProcessor(whisperFactory);
        await using var audioStream = File.OpenRead(audioFullPath);

        var transcript = new StringBuilder();
        await foreach (var segment in processor.ProcessAsync(audioStream, cancellationToken))
        {
            transcript.Append(segment.Text);
        }

        return CleanTranscript(transcript.ToString());
    }

    private async Task<string> GetModelPathAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_transcriptionOptions.ModelPath))
        {
            var configuredModelPath = Path.GetFullPath(_transcriptionOptions.ModelPath);
            if (!File.Exists(configuredModelPath))
            {
                throw new FileNotFoundException("Configured Whisper model file was not found.", configuredModelPath);
            }

            return configuredModelPath;
        }

        var modelDirectory = GetSafeStorageFullPath(_transcriptionOptions.ModelDirectory);
        Directory.CreateDirectory(modelDirectory);

        var modelType = GetModelType(_transcriptionOptions.ModelSize);
        var modelPath = Path.Combine(modelDirectory, $"ggml-{_transcriptionOptions.ModelSize.ToLowerInvariant()}.bin");
        if (IsUsableModelFile(modelPath))
        {
            return modelPath;
        }

        DeleteInvalidModelFile(modelPath);

        if (!_transcriptionOptions.AutoDownloadModel)
        {
            throw new InvalidOperationException(
                "Whisper model file was not found and automatic model download is disabled.");
        }

        var temporaryModelPath = $"{modelPath}.download";
        if (IsUsableModelFile(temporaryModelPath))
        {
            File.Move(temporaryModelPath, modelPath, overwrite: true);
            return modelPath;
        }

        DeleteInvalidModelFile(temporaryModelPath);

        await using (var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(
                         modelType,
                         cancellationToken: cancellationToken))
        {
            await using var fileStream = new FileStream(
                temporaryModelPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await modelStream.CopyToAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
        }

        if (!IsUsableModelFile(temporaryModelPath))
        {
            DeleteInvalidModelFile(temporaryModelPath);
            throw new InvalidOperationException("Downloaded Whisper model file is empty or incomplete.");
        }

        File.Move(temporaryModelPath, modelPath, overwrite: true);
        return modelPath;
    }

    private WhisperProcessor CreateProcessor(WhisperFactory whisperFactory)
    {
        var builder = whisperFactory.CreateBuilder();
        if (string.Equals(_transcriptionOptions.Language, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return builder.WithLanguageDetection().Build();
        }

        return builder.WithLanguage(_transcriptionOptions.Language).Build();
    }

    private string GetSafeStorageFullPath(string relativePath)
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

    private static GgmlType GetModelType(string modelSize)
    {
        return modelSize.Trim().ToLowerInvariant() switch
        {
            "tiny" => GgmlType.Tiny,
            "base" => GgmlType.Base,
            "small" => GgmlType.Small,
            "medium" => GgmlType.Medium,
            "large-v1" => GgmlType.LargeV1,
            "large-v2" => GgmlType.LargeV2,
            "large-v3" => GgmlType.LargeV3,
            "large-v3-turbo" => GgmlType.LargeV3Turbo,
            _ => GgmlType.Small
        };
    }

    private static bool IsUsableModelFile(string modelPath)
    {
        return File.Exists(modelPath) && new FileInfo(modelPath).Length >= MinimumModelSizeBytes;
    }

    private static void DeleteInvalidModelFile(string modelPath)
    {
        if (File.Exists(modelPath) && !IsUsableModelFile(modelPath))
        {
            File.Delete(modelPath);
        }
    }

    private static string CleanTranscript(string transcript)
    {
        return transcript
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim();
    }
}

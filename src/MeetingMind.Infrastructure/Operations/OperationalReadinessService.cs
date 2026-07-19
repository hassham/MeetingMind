using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Operations;
using MeetingMind.Application.Common.Options;
using MeetingMind.Infrastructure.Persistence;

namespace MeetingMind.Infrastructure.Operations;

public sealed class OperationalReadinessService : IOperationalReadinessService
{
    private readonly MeetingMindDbContext _dbContext;
    private readonly StorageOptions _storageOptions;
    private readonly AudioProcessingOptions _audioOptions;
    private readonly TranscriptionOptions _transcriptionOptions;

    public OperationalReadinessService(
        MeetingMindDbContext dbContext,
        StorageOptions storageOptions,
        AudioProcessingOptions audioOptions,
        TranscriptionOptions transcriptionOptions)
    {
        _dbContext = dbContext;
        _storageOptions = storageOptions;
        _audioOptions = audioOptions;
        _transcriptionOptions = transcriptionOptions;
    }

    public async Task<IReadOnlyList<ReadinessCheckResult>> CheckAsync(
        CancellationToken cancellationToken)
    {
        return
        [
            new ReadinessCheckResult("database", await CheckDatabaseAsync(cancellationToken)),
            new ReadinessCheckResult("storage", await CheckStorageAsync(cancellationToken)),
            new ReadinessCheckResult("ffmpeg", CheckFfmpeg()),
            new ReadinessCheckResult("whisper_model", CheckWhisperModel())
        ];
    }

    private async Task<bool> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckStorageAsync(CancellationToken cancellationToken)
    {
        string? probePath = null;
        try
        {
            var healthDirectory = Path.Combine(_storageOptions.RootPath, ".health");
            Directory.CreateDirectory(healthDirectory);
            probePath = Path.Combine(healthDirectory, $"{Guid.NewGuid():N}.probe");
            await File.WriteAllBytesAsync(probePath, [], cancellationToken);
            File.Delete(probePath);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (probePath is not null)
            {
                try
                {
                    File.Delete(probePath);
                }
                catch
                {
                    // A failed probe is reported as unhealthy without exposing its physical path.
                }
            }
        }
    }

    private bool CheckFfmpeg()
    {
        if (string.IsNullOrWhiteSpace(_audioOptions.FfmpegBinaryFolder))
        {
            return false;
        }

        try
        {
            var configuredPath = Path.GetFullPath(_audioOptions.FfmpegBinaryFolder);
            if (File.Exists(configuredPath))
            {
                return IsFfmpegFile(configuredPath);
            }

            return Directory.Exists(configuredPath) &&
                   (File.Exists(Path.Combine(configuredPath, "ffmpeg")) ||
                    File.Exists(Path.Combine(configuredPath, "ffmpeg.exe")));
        }
        catch
        {
            return false;
        }
    }

    private bool CheckWhisperModel()
    {
        try
        {
            var modelPath = !string.IsNullOrWhiteSpace(_transcriptionOptions.ModelPath)
                ? Path.GetFullPath(_transcriptionOptions.ModelPath)
                : Path.GetFullPath(Path.Combine(
                    _storageOptions.RootPath,
                    _transcriptionOptions.ModelDirectory,
                    $"ggml-{_transcriptionOptions.ModelSize.Trim().ToLowerInvariant()}.bin"));

            using var stream = new FileStream(
                modelPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            return stream.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFfmpegFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase);
    }
}

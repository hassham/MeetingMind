using FFMpegCore.Exceptions;
using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Failures;
using MeetingMind.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.ClientModel;
using System.Net;
using System.Text.Json;

namespace MeetingMind.Infrastructure.Failures;

public sealed class MeetingFailureClassifier : IMeetingFailureClassifier
{
    private const int ErrorDiskFull = unchecked((int)0x80070070);
    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);

    public MeetingFailureClassification Classify(Exception exception)
    {
        var exceptions = Enumerate(exception).ToArray();

        if (exceptions.OfType<PermanentMeetingProcessingException>().Any())
        {
            return Permanent("Meeting processing cannot continue without correcting its input or configuration.");
        }

        var clientException = exceptions.OfType<ClientResultException>().FirstOrDefault();
        if (clientException is not null)
        {
            if (IsNonRetryableQuotaFailure(clientException) ||
                clientException.Status is >= 400 and < 500 and not 408 and not 409 and not 429)
            {
                return Permanent("The meeting-minutes provider rejected the request.");
            }

            if (clientException.Status is 408 or 409 or 429 || clientException.Status >= 500)
            {
                return Transient("The meeting-minutes provider is temporarily unavailable.");
            }
        }

        var databaseException = exceptions.OfType<NpgsqlException>().FirstOrDefault();
        if (databaseException is not null)
        {
            return databaseException.IsTransient
                ? Transient("Meeting data storage is temporarily unavailable.")
                : Permanent("Meeting data could not be saved because of a permanent database error.");
        }

        if (exceptions.OfType<DbUpdateException>().Any())
        {
            return Permanent("Meeting data could not be saved because it was invalid or conflicted with existing data.");
        }

        if (exceptions.Any(current => current is FileNotFoundException or DirectoryNotFoundException))
        {
            return Permanent("A required meeting-processing file was not found.");
        }

        if (exceptions.Any(current => current is UnauthorizedAccessException or PathTooLongException))
        {
            return Permanent("Meeting processing cannot access a required local resource.");
        }

        if (exceptions.OfType<IOException>().Any(io => io.HResult is ErrorDiskFull or ErrorHandleDiskFull))
        {
            return Permanent("Local storage does not have enough available space.");
        }

        if (exceptions.Any(current => current is TimeoutException or HttpRequestException or OperationCanceledException))
        {
            return Transient("A temporary timeout or network interruption stopped meeting processing.");
        }

        if (exceptions.OfType<IOException>().Any())
        {
            return Transient("Local storage is temporarily unavailable.");
        }

        if (exceptions.Any(current => current is FFMpegArgumentException or
                                      FFMpegStreamFormatException or
                                      FFOptionsException or
                                      FormatNullException or
                                      FFMpegException))
        {
            return Permanent("The uploaded audio could not be processed by FFmpeg.");
        }

        if (exceptions.Any(current => current is ArgumentException or NotSupportedException))
        {
            return Permanent("Meeting processing received unsupported or invalid input.");
        }

        if (exceptions.OfType<JsonException>().Any())
        {
            return Permanent("The meeting-minutes provider returned an invalid structured response.");
        }

        return Transient("Meeting processing failed temporarily and will be retried.");
    }

    private static IEnumerable<Exception> Enumerate(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            yield return current;
        }
    }

    private static bool IsNonRetryableQuotaFailure(ClientResultException exception)
    {
        return exception.Status == (int)HttpStatusCode.TooManyRequests &&
               (exception.Message.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("billing", StringComparison.OrdinalIgnoreCase));
    }

    private static MeetingFailureClassification Transient(string message) =>
        new(MeetingFailureKind.Transient, message);

    private static MeetingFailureClassification Permanent(string message) =>
        new(MeetingFailureKind.Permanent, message);
}

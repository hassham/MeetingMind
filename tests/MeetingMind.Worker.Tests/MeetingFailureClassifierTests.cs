using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Failures;
using MeetingMind.Infrastructure.Failures;
using System.Text.Json;

namespace MeetingMind.Worker.Tests;

public sealed class MeetingFailureClassifierTests
{
    private readonly MeetingFailureClassifier _classifier = new();

    [Fact]
    public void ExplicitPermanentFailureIsNotRetryable()
    {
        var result = _classifier.Classify(
            new PermanentMeetingProcessingException(
                "invalid input",
                new InvalidOperationException("provider detail")));

        Assert.Equal(MeetingFailureKind.Permanent, result.Kind);
        Assert.Equal(MeetingErrorCodes.InvalidInput, result.ErrorCode);
        Assert.DoesNotContain("provider detail", result.SafeMessage);
    }

    [Fact]
    public void MissingFilesAndAccessFailuresArePermanent()
    {
        Assert.Equal(
            MeetingFailureKind.Permanent,
            _classifier.Classify(new FileNotFoundException("local path")).Kind);
        Assert.Equal(
            MeetingFailureKind.Permanent,
            _classifier.Classify(new UnauthorizedAccessException("local path")).Kind);
    }

    [Fact]
    public void TemporaryIoNetworkAndTimeoutFailuresAreTransient()
    {
        Assert.Equal(
            MeetingFailureKind.Transient,
            _classifier.Classify(new IOException("sharing violation")).Kind);
        Assert.Equal(
            MeetingFailureKind.Transient,
            _classifier.Classify(new HttpRequestException("network interruption")).Kind);
        Assert.Equal(
            MeetingFailureKind.Transient,
            _classifier.Classify(new TimeoutException("provider timeout")).Kind);
    }

    [Fact]
    public void UnknownFailuresUseTheApprovedTransientDefault()
    {
        var result = _classifier.Classify(new InvalidOperationException("unexpected defect detail"));

        Assert.Equal(MeetingFailureKind.Transient, result.Kind);
        Assert.Equal(MeetingErrorCodes.UnexpectedFailure, result.ErrorCode);
        Assert.Equal("Meeting processing failed temporarily and will be retried.", result.SafeMessage);
    }

    [Fact]
    public void MalformedStructuredProviderOutputIsPermanent()
    {
        var result = _classifier.Classify(
            new InvalidOperationException("provider wrapper", new JsonException("payload detail")));

        Assert.Equal(MeetingFailureKind.Permanent, result.Kind);
        Assert.DoesNotContain("payload detail", result.SafeMessage);
    }
}

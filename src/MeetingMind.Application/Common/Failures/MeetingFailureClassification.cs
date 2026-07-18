namespace MeetingMind.Application.Common.Failures;

public sealed record MeetingFailureClassification(
    MeetingFailureKind Kind,
    string SafeMessage);

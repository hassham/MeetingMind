namespace MeetingMind.Application.Common.Exceptions;

public sealed class MeetingProcessingAttemptException : Exception
{
    public MeetingProcessingAttemptException(string errorCode, string safeMessage)
        : base($"{errorCode}: {safeMessage}")
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

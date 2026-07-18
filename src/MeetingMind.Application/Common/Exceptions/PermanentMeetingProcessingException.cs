namespace MeetingMind.Application.Common.Exceptions;

public sealed class PermanentMeetingProcessingException : Exception
{
    public PermanentMeetingProcessingException(string message)
        : base(message)
    {
    }

    public PermanentMeetingProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

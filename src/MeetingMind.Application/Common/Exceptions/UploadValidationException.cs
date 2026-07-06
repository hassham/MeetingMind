namespace MeetingMind.Application.Common.Exceptions;

public class UploadValidationException : Exception
{
    public UploadValidationException(string message)
        : base(message)
    {
    }
}

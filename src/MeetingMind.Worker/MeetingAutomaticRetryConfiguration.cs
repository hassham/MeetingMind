using Hangfire;
using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Options;

namespace MeetingMind.Worker;

public static class MeetingAutomaticRetryConfiguration
{
    public static AutomaticRetryAttribute CreateFilter(AutomaticRetryOptions options)
    {
        return new AutomaticRetryAttribute
        {
            Attempts = options.RetryLimit,
            DelaysInSeconds = options.DelaysInSeconds,
            ExceptOn = [typeof(PermanentMeetingProcessingException)],
            OnAttemptsExceeded = AttemptsExceededAction.Fail,
            LogEvents = true
        };
    }
}

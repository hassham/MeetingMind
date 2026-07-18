namespace MeetingMind.Application.Common.Options;

public class AutomaticRetryOptions
{
    public int[] DelaysInSeconds { get; set; } = [10, 60];

    public int RetryLimit => DelaysInSeconds.Length;
}

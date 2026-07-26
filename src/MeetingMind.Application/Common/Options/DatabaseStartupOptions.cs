namespace MeetingMind.Application.Common.Options;

public sealed class DatabaseStartupOptions
{
    public int MaxAttempts { get; set; } = 5;

    public int DelaySeconds { get; set; } = 2;
}

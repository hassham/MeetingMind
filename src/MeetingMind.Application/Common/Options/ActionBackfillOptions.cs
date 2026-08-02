namespace MeetingMind.Application.Common.Options;

public sealed class ActionBackfillOptions
{
    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 100;
}

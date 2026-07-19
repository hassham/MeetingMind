namespace MeetingMind.Application.Common.Options;

public sealed class StorageRetentionOptions
{
    public bool Enabled { get; set; }

    public int RetentionDays { get; set; } = 30;

    public string Schedule { get; set; } = "0 2 * * *";

    public int BatchSize { get; set; } = 100;
}

namespace MeetingMind.Worker.Jobs;

public interface IStorageRetentionJob
{
    Task RunAsync();
}

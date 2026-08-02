using Hangfire;
using MeetingMind.Application.Actions;
using MeetingMind.Application.Common.Options;

namespace MeetingMind.Worker.Jobs;

public sealed class ActionBackfillJob : IActionBackfillJob
{
    private readonly IActionService _actions;
    private readonly ActionBackfillOptions _options;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<ActionBackfillJob> _logger;
    public ActionBackfillJob(IActionService actions, ActionBackfillOptions options, IBackgroundJobClient jobs, ILogger<ActionBackfillJob> logger) { _actions = actions; _options = options; _jobs = jobs; _logger = logger; }
    public async Task RunAsync()
    {
        var result = await _actions.BackfillAsync(_options.BatchSize, CancellationToken.None);
        _logger.LogInformation("Action backfill processed {Meetings} meetings and created {Actions} actions", result.ProcessedMeetings, result.CreatedActions);
        if (result.HasMore) _jobs.Enqueue<IActionBackfillJob>(job => job.RunAsync());
    }
}

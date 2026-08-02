using MeetingMind.Application.Actions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Unit.Tests;

public sealed class ActionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateTrimsFieldsAndUsesManualOpenDefaults()
    {
        var repository = new StubRepository();
        var result = await Service(repository).CreateAsync(new("  Ship it  ", " Alex ", null, new DateOnly(2026, 8, 1), null), default);
        Assert.Equal("Ship it", result.Description);
        Assert.Equal("Alex", result.Assignee);
        Assert.Equal(ActionItemStatus.Open, result.Status);
        Assert.Equal(ActionItemSource.Manual, result.Source);
        Assert.True(result.IsOverdue);
    }

    [Fact]
    public async Task UpdateSetsCompletionOnceAndRejectsStaleVersion()
    {
        var repository = new StubRepository();
        var created = await Service(repository).CreateAsync(new("Task", null, null, null, null), default);
        var completed = await Service(repository).UpdateAsync(created.Id, new("Task", null, null, null, ActionItemStatus.Completed, null, created.Version), default);
        Assert.Equal(Now, completed.CompletedAt);
        await Assert.ThrowsAsync<ActionConflictException>(() => Service(repository).UpdateAsync(created.Id, new("Changed", null, null, null, ActionItemStatus.Open, null, created.Version), default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2001)]
    public async Task CreateRejectsInvalidDescriptionLength(int length)
    {
        await Assert.ThrowsAsync<ActionValidationException>(() => Service(new StubRepository()).CreateAsync(new(new string('x', length), null, null, null, null), default));
    }

    private static ActionService Service(StubRepository repository) => new(repository, new StubExporter(), new FixedTimeProvider(Now));

    private sealed class StubRepository : IActionRepository
    {
        private readonly Dictionary<Guid, ActionItem> _items = [];
        public Task<ActionItem> CreateAsync(ActionItem item, CancellationToken ct) { _items[item.Id] = item; return Task.FromResult(item); }
        public Task<ActionItem?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(_items.GetValueOrDefault(id));
        public Task<ActionItem?> UpdateAsync(ActionItem item, long expected, CancellationToken ct) { if (!_items.TryGetValue(item.Id, out var stored) || stored.Version != expected) return Task.FromResult<ActionItem?>(null); item.Version = expected + 1; _items[item.Id] = item; return Task.FromResult<ActionItem?>(item); }
        public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => Task.FromResult(_items.Remove(id));
        public Task<(IReadOnlyList<ActionItem> Items, int Total)> ListAsync(ActionQuery query, DateOnly today, CancellationToken ct) => Task.FromResult<(IReadOnlyList<ActionItem> Items, int Total)>((_items.Values.ToArray(), _items.Count));
        public Task<(string Title, string SourceFileName)?> GetMeetingProvenanceAsync(Guid id, CancellationToken ct) => Task.FromResult<(string, string)?>(null);
        public Task<int> SeedGeneratedAsync(Guid id, CancellationToken ct) => Task.FromResult(0);
        public Task<ActionBackfillResult> BackfillAsync(int size, CancellationToken ct) => Task.FromResult(new ActionBackfillResult(0, 0, false));
        public Task<IReadOnlyList<ActionItem>> ExportAsync(ActionExportRequest request, int max, DateOnly today, CancellationToken ct) => Task.FromResult<IReadOnlyList<ActionItem>>(_items.Values.Take(max).ToArray());
    }

    private sealed class StubExporter : IActionItemExporter
    {
        public ActionExportFile Export(string format, IReadOnlyList<ActionItem> actions) => new([], "test", "test");
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}

using System.Globalization;
using System.Text.Json;
using MeetingMind.Application.Actions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeetingMind.Infrastructure.Persistence;

public sealed class EfActionRepository : IActionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly MeetingMindDbContext _db;
    private readonly TimeProvider _timeProvider;
    public EfActionRepository(MeetingMindDbContext db, TimeProvider timeProvider) { _db = db; _timeProvider = timeProvider; }

    public async Task<(IReadOnlyList<ActionItem> Items, int Total)> ListAsync(ActionQuery query, DateOnly today, CancellationToken ct)
    {
        var source = Filter(_db.ActionItems.AsNoTracking(), query.Status, query.Assignee, query.Due, query.Source, query.MeetingId, today);
        var total = await source.CountAsync(ct);
        var items = await source.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Skip(query.Skip).Take(query.Take).ToArrayAsync(ct);
        return (items, total);
    }

    public Task<ActionItem?> GetAsync(Guid id, CancellationToken ct) => _db.ActionItems.SingleOrDefaultAsync(a => a.Id == id, ct);
    public async Task<ActionItem> CreateAsync(ActionItem item, CancellationToken ct) { _db.ActionItems.Add(item); await _db.SaveChangesAsync(ct); return item; }
    public async Task<ActionItem?> UpdateAsync(ActionItem item, long expectedVersion, CancellationToken ct)
    {
        _db.Entry(item).Property(a => a.Version).OriginalValue = expectedVersion;
        item.Version = expectedVersion + 1;
        try { await _db.SaveChangesAsync(ct); return item; }
        catch (DbUpdateConcurrencyException) { _db.ChangeTracker.Clear(); return null; }
    }
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct) { var count = await _db.ActionItems.Where(a => a.Id == id).ExecuteDeleteAsync(ct); return count == 1; }

    public async Task<(string Title, string SourceFileName)?> GetMeetingProvenanceAsync(Guid id, CancellationToken ct)
    {
        var value = await _db.MeetingJobs.AsNoTracking().Where(j => j.Id == id)
            .Select(j => new { Title = j.Minutes != null ? j.Minutes.Title : j.OriginalFileName, SourceFileName = j.OriginalFileName })
            .SingleOrDefaultAsync(ct);
        return value is null ? null : (value.Title, value.SourceFileName);
    }

    public async Task<int> SeedGeneratedAsync(Guid meetingId, CancellationToken ct)
    {
        var minutes = await _db.MeetingMinutes.Include(m => m.MeetingJob).SingleOrDefaultAsync(m => m.MeetingJobId == meetingId, ct);
        var source = minutes is null ? null : new { minutes.MeetingJobId, minutes.Title, minutes.ActionItemsJson, SourceFileName = minutes.MeetingJob!.OriginalFileName };
        if (source is null) return 0;
        var items = Deserialize(source.ActionItemsJson);
        var keys = items.Select((_, index) => Key(meetingId, index)).ToArray();
        var existing = await _db.ActionItems.Where(a => a.GeneratedSourceKey != null && keys.Contains(a.GeneratedSourceKey)).Select(a => a.GeneratedSourceKey!).ToHashSetAsync(ct);
        var now = _timeProvider.GetUtcNow();
        var additions = items.Select((item, index) => (item, index)).Where(x => !existing.Contains(Key(meetingId, x.index))).Select(x => new ActionItem
        {
            Id = Guid.NewGuid(), Description = NormalizeDescription(x.item.Description), Assignee = Trim(x.item.Owner, 200), DueDate = ParseDueDate(x.item.DueDate), Status = ActionItemStatus.Open, Source = ActionItemSource.Generated, MeetingJobId = meetingId, ProvenanceMeetingTitle = source.Title, ProvenanceSourceFileName = source.SourceFileName, GeneratedSourceKey = Key(meetingId, x.index), CreatedAt = now, UpdatedAt = now, Version = 1
        }).ToArray();
        if (additions.Length > 0) _db.ActionItems.AddRange(additions);
        minutes!.ActionsSeededAt = now;
        try { await _db.SaveChangesAsync(ct); return additions.Length; }
        catch (DbUpdateException) { _db.ChangeTracker.Clear(); return 0; }
    }

    public async Task<ActionBackfillResult> BackfillAsync(int batchSize, CancellationToken ct)
    {
        var meetingIds = await _db.MeetingMinutes.AsNoTracking()
            .Where(m => m.ActionsSeededAt == null)
            .OrderBy(m => m.CreatedAt).Select(m => m.MeetingJobId).Take(batchSize + 1).ToArrayAsync(ct);
        var processed = meetingIds.Take(batchSize).ToArray();
        var created = 0;
        foreach (var id in processed) created += await SeedGeneratedAsync(id, ct);
        return new(processed.Length, created, meetingIds.Length > batchSize);
    }

    public async Task<IReadOnlyList<ActionItem>> ExportAsync(ActionExportRequest request, int maximumRows, DateOnly today, CancellationToken ct)
    {
        IQueryable<ActionItem> query = _db.ActionItems.AsNoTracking();
        if (request.Ids is { Count: > 0 }) query = query.Where(a => request.Ids.Contains(a.Id));
        else query = Filter(query, request.Status, request.Assignee, request.Due, request.Source, request.MeetingId, today);
        return await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Take(maximumRows).ToArrayAsync(ct);
    }

    private static IQueryable<ActionItem> Filter(IQueryable<ActionItem> query, ActionItemStatus? status, string? assignee, string? due, ActionItemSource? source, Guid? meetingId, DateOnly today)
    {
        if (status is not null) query = query.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(assignee)) { var term = assignee.Trim().ToLower(); query = query.Where(a => a.Assignee != null && a.Assignee.ToLower().Contains(term)); }
        if (source is not null) query = query.Where(a => a.Source == source);
        if (meetingId is not null) query = query.Where(a => a.MeetingJobId == meetingId);
        query = due switch { "overdue" => query.Where(a => a.DueDate < today && a.Status != ActionItemStatus.Completed && a.Status != ActionItemStatus.Cancelled), "due" => query.Where(a => a.DueDate != null), "none" => query.Where(a => a.DueDate == null), _ => query };
        return query;
    }

    private static IReadOnlyList<MeetingActionItem> Deserialize(string json) { try { return JsonSerializer.Deserialize<List<MeetingActionItem>>(json, JsonOptions) ?? []; } catch (JsonException) { return []; } }
    private static string Key(Guid id, int index) => $"meeting:{id:N}:action:{index}";
    private static DateOnly? ParseDueDate(string? value) => DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    private static string NormalizeDescription(string? value) { var trimmed = value?.Trim(); return string.IsNullOrEmpty(trimmed) ? "Generated action" : trimmed[..Math.Min(2000, trimmed.Length)]; }
    private static string? Trim(string? value, int max) { var text = value?.Trim(); return string.IsNullOrEmpty(text) ? null : text[..Math.Min(max, text.Length)]; }
}

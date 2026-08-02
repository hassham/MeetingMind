using System.Text;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Application.Actions;

public sealed class ActionService : IActionService
{
    public const int MaximumSelectedExport = 100;
    public const int MaximumFilteredExport = 5000;
    private readonly IActionRepository _repository;
    private readonly IActionItemExporter _exporter;
    private readonly TimeProvider _timeProvider;

    public ActionService(IActionRepository repository, IActionItemExporter exporter, TimeProvider timeProvider)
    {
        _repository = repository;
        _exporter = exporter;
        _timeProvider = timeProvider;
    }

    public async Task<ActionListResult> ListAsync(ActionQuery query, CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var (items, total) = await _repository.ListAsync(query, today, cancellationToken);
        return new(items.Select(item => View(item, today)).ToArray(), query.Skip, query.Take, total);
    }

    public async Task<ActionItemView> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await _repository.GetAsync(id, cancellationToken) ?? throw new ActionNotFoundException();
        return View(item, UtcToday());
    }

    public async Task<ActionItemView> CreateAsync(CreateActionRequest request, CancellationToken cancellationToken)
    {
        var provenance = await ResolveMeetingAsync(request.MeetingId, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var item = new ActionItem
        {
            Id = Guid.NewGuid(), Description = Required(request.Description, 2000, "Description"),
            Assignee = Optional(request.Assignee, 200, "Assignee"), Notes = Optional(request.Notes, 10000, "Notes"),
            DueDate = request.DueDate, Status = ActionItemStatus.Open, Source = ActionItemSource.Manual,
            MeetingJobId = request.MeetingId, ProvenanceMeetingTitle = provenance?.Title,
            ProvenanceSourceFileName = provenance?.SourceFileName, CreatedAt = now, UpdatedAt = now, Version = 1
        };
        return View(await _repository.CreateAsync(item, cancellationToken), UtcToday());
    }

    public async Task<ActionItemView> UpdateAsync(Guid id, UpdateActionRequest request, CancellationToken cancellationToken)
    {
        var current = await _repository.GetAsync(id, cancellationToken) ?? throw new ActionNotFoundException();
        var expected = DecodeVersion(request.Version);
        if (expected != current.Version) throw new ActionConflictException();
        var provenance = request.MeetingId == current.MeetingJobId
            ? null : await ResolveMeetingAsync(request.MeetingId, cancellationToken);
        var wasCompleted = current.Status == ActionItemStatus.Completed;
        current.Description = Required(request.Description, 2000, "Description");
        current.Assignee = Optional(request.Assignee, 200, "Assignee");
        current.Notes = Optional(request.Notes, 10000, "Notes");
        current.DueDate = request.DueDate;
        current.Status = request.Status;
        current.MeetingJobId = request.MeetingId;
        if (provenance is not null)
        {
            current.ProvenanceMeetingTitle = provenance.Value.Title;
            current.ProvenanceSourceFileName = provenance.Value.SourceFileName;
        }
        current.CompletedAt = request.Status == ActionItemStatus.Completed
            ? wasCompleted ? current.CompletedAt : _timeProvider.GetUtcNow()
            : null;
        current.UpdatedAt = _timeProvider.GetUtcNow();
        var updated = await _repository.UpdateAsync(current, expected, cancellationToken)
            ?? throw new ActionConflictException();
        return View(updated, UtcToday());
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await _repository.DeleteAsync(id, cancellationToken)) throw new ActionNotFoundException();
    }

    public async Task<ActionExportFile> ExportAsync(ActionExportRequest request, CancellationToken cancellationToken)
    {
        if (request.Format is not ("csv" or "json")) throw new ActionValidationException("Format must be csv or json.");
        if (request.Ids?.Count > MaximumSelectedExport) throw new ActionValidationException("At most 100 action IDs may be exported.");
        var rows = await _repository.ExportAsync(request, MaximumFilteredExport + 1, UtcToday(), cancellationToken);
        if (rows.Count > MaximumFilteredExport) throw new ActionValidationException("The export exceeds the 5000 row limit. Narrow the filters.");
        return _exporter.Export(request.Format, rows);
    }

    public Task<int> SeedGeneratedAsync(Guid meetingId, CancellationToken cancellationToken) => _repository.SeedGeneratedAsync(meetingId, cancellationToken);
    public Task<ActionBackfillResult> BackfillAsync(int batchSize, CancellationToken cancellationToken) => _repository.BackfillAsync(Math.Clamp(batchSize, 1, 500), cancellationToken);

    private async Task<(string Title, string SourceFileName)?> ResolveMeetingAsync(Guid? id, CancellationToken ct)
    {
        if (id is null) return null;
        return await _repository.GetMeetingProvenanceAsync(id.Value, ct)
            ?? throw new ActionValidationException("The linked meeting does not exist.");
    }

    private static void ValidateQuery(ActionQuery query)
    {
        if (query.Skip < 0 || query.Take is < 1 or > 100) throw new ActionValidationException("Skip must be non-negative and take must be between 1 and 100.");
        if (query.Due is not null && query.Due is not ("due" or "overdue" or "none")) throw new ActionValidationException("Due must be due, overdue, or none.");
    }

    private DateOnly UtcToday() => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
    private static ActionItemView View(ActionItem a, DateOnly today) => new(a.Id, a.Description, a.Assignee, a.Notes, a.DueDate, a.Status, a.Source, a.MeetingJobId, a.ProvenanceMeetingTitle, a.ProvenanceSourceFileName, a.CreatedAt, a.UpdatedAt, a.CompletedAt, EncodeVersion(a.Version), a.DueDate < today && a.Status is not (ActionItemStatus.Completed or ActionItemStatus.Cancelled));
    private static string Required(string value, int max, string name) { var result = value?.Trim() ?? ""; if (result.Length is < 1 || result.Length > max) throw new ActionValidationException($"{name} must be between 1 and {max} characters."); return result; }
    private static string? Optional(string? value, int max, string name) { var result = value?.Trim(); if (string.IsNullOrEmpty(result)) return null; if (result.Length > max) throw new ActionValidationException($"{name} must not exceed {max} characters."); return result; }
    private static string EncodeVersion(long version) => Convert.ToBase64String(Encoding.UTF8.GetBytes(version.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    private static long DecodeVersion(string version) { try { return long.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(version)), System.Globalization.CultureInfo.InvariantCulture); } catch { throw new ActionValidationException("Version is invalid."); } }
}

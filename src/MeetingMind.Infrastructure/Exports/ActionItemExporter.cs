using System.Text;
using System.Text.Json;
using MeetingMind.Application.Actions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Domain.Entities;

namespace MeetingMind.Infrastructure.Exports;

public sealed class ActionItemExporter : IActionItemExporter
{
    public ActionExportFile Export(string format, IReadOnlyList<ActionItem> actions) => format == "json" ? Json(actions) : Csv(actions);

    private static ActionExportFile Json(IReadOnlyList<ActionItem> actions)
    {
        var payload = new
        {
            schemaVersion = "1.0",
            exportedAtUtc = DateTimeOffset.UtcNow,
            actions = actions.Select(a => new { a.Id, a.Description, a.Assignee, a.Notes, dueDate = a.DueDate?.ToString("yyyy-MM-dd"), status = a.Status.ToString(), source = a.Source.ToString(), meetingId = a.MeetingJobId, meetingTitle = a.ProvenanceMeetingTitle, sourceFileName = a.ProvenanceSourceFileName, a.CreatedAt, a.UpdatedAt, a.CompletedAt })
        };
        return new(JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true }), "application/json; charset=utf-8", "meetingmind-actions.json");
    }

    private static ActionExportFile Csv(IReadOnlyList<ActionItem> actions)
    {
        var text = new StringBuilder("Id,Description,Assignee,Notes,DueDate,Status,Source,MeetingId,MeetingTitle,SourceFileName,CreatedAt,UpdatedAt,CompletedAt\r\n");
        foreach (var a in actions)
        {
            var cells = new[] { a.Id.ToString(), a.Description, a.Assignee, a.Notes, a.DueDate?.ToString("yyyy-MM-dd"), a.Status.ToString(), a.Source.ToString(), a.MeetingJobId?.ToString(), a.ProvenanceMeetingTitle, a.ProvenanceSourceFileName, a.CreatedAt.ToString("O"), a.UpdatedAt.ToString("O"), a.CompletedAt?.ToString("O") };
            text.AppendJoin(',', cells.Select(Cell)).Append("\r\n");
        }
        return new(new UTF8Encoding(true).GetBytes(text.ToString()), "text/csv; charset=utf-8", "meetingmind-actions.csv");
    }

    private static string Cell(string? value)
    {
        var safe = value ?? string.Empty;
        var significant = safe.TrimStart();
        if (significant.Length > 0 && "=+-@\t\r".Contains(significant[0])) safe = "'" + safe;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}

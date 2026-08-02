using MeetingMind.Application.Actions;
using MeetingMind.Domain.Entities;

namespace MeetingMind.Application.Common.Interfaces;

public interface IActionItemExporter
{
    ActionExportFile Export(string format, IReadOnlyList<ActionItem> actions);
}

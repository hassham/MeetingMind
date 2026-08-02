using MeetingMind.Application.Actions;
using MeetingMind.Domain.Entities;

namespace MeetingMind.Application.Common.Interfaces;

public interface IActionRepository
{
    Task<(IReadOnlyList<ActionItem> Items, int Total)> ListAsync(ActionQuery query, DateOnly utcToday, CancellationToken cancellationToken);
    Task<ActionItem?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ActionItem> CreateAsync(ActionItem item, CancellationToken cancellationToken);
    Task<ActionItem?> UpdateAsync(ActionItem item, long expectedVersion, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<(string Title, string SourceFileName)?> GetMeetingProvenanceAsync(Guid meetingId, CancellationToken cancellationToken);
    Task<int> SeedGeneratedAsync(Guid meetingId, CancellationToken cancellationToken);
    Task<ActionBackfillResult> BackfillAsync(int batchSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionItem>> ExportAsync(ActionExportRequest request, int maximumRows, DateOnly utcToday, CancellationToken cancellationToken);
}

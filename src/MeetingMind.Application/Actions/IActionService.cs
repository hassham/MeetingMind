namespace MeetingMind.Application.Actions;

public interface IActionService
{
    Task<ActionListResult> ListAsync(ActionQuery query, CancellationToken cancellationToken);
    Task<ActionItemView> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ActionItemView> CreateAsync(CreateActionRequest request, CancellationToken cancellationToken);
    Task<ActionItemView> UpdateAsync(Guid id, UpdateActionRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<ActionExportFile> ExportAsync(ActionExportRequest request, CancellationToken cancellationToken);
    Task<int> SeedGeneratedAsync(Guid meetingId, CancellationToken cancellationToken);
    Task<ActionBackfillResult> BackfillAsync(int batchSize, CancellationToken cancellationToken);
}

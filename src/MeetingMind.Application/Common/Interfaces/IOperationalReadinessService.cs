using MeetingMind.Application.Common.Operations;

namespace MeetingMind.Application.Common.Interfaces;

public interface IOperationalReadinessService
{
    Task<IReadOnlyList<ReadinessCheckResult>> CheckAsync(CancellationToken cancellationToken);
}

using MeetingMind.Domain.Entities;

namespace MeetingMind.Application.Common.Interfaces;

public interface IMeetingJobRepository
{
    Task AddAsync(MeetingJob meetingJob, CancellationToken cancellationToken);
}

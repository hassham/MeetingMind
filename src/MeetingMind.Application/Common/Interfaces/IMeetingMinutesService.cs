using MeetingMind.Application.Meetings;

namespace MeetingMind.Application.Common.Interfaces;

public interface IMeetingMinutesService
{
    Task<MeetingMinutesContent> GenerateMinutesAsync(
        string transcriptText,
        CancellationToken cancellationToken);
}

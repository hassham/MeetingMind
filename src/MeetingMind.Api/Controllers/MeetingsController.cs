using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Meetings;
using Microsoft.AspNetCore.Mvc;

namespace MeetingMind.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeetingsController : ControllerBase
{
    private readonly IUploadMeetingService _uploadMeetingService;
    private readonly IMeetingStatusService _meetingStatusService;
    private readonly IMeetingTranscriptService _meetingTranscriptService;
    private readonly IMeetingMinutesResultService _meetingMinutesResultService;

    public MeetingsController(
        IUploadMeetingService uploadMeetingService,
        IMeetingStatusService meetingStatusService,
        IMeetingTranscriptService meetingTranscriptService,
        IMeetingMinutesResultService meetingMinutesResultService)
    {
        _uploadMeetingService = uploadMeetingService;
        _meetingStatusService = meetingStatusService;
        _meetingTranscriptService = meetingTranscriptService;
        _meetingMinutesResultService = meetingMinutesResultService;
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var request = new UploadMeetingRequest(
            stream,
            file.FileName,
            file.ContentType,
            file.Length);

        try
        {
            var result = await _uploadMeetingService.UploadAsync(request, cancellationToken);

            return Accepted($"/api/meetings/{result.JobId}/status", new
            {
                jobId = result.JobId,
                status = result.Status.ToString(),
                stage = result.Stage.ToString()
            });
        }
        catch (UploadValidationException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
    }

    [HttpGet("{jobId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await _meetingStatusService.GetStatusAsync(jobId, cancellationToken);
        if (result is null)
        {
            return NotFound(new
            {
                error = "Meeting job not found."
            });
        }

        return Ok(new
        {
            jobId = result.JobId,
            status = result.Status,
            stage = result.Stage,
            progress = result.Progress,
            errorMessage = result.ErrorMessage
        });
    }

    [HttpGet("{jobId:guid}/transcript/download")]
    public async Task<IActionResult> DownloadTranscript(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await _meetingTranscriptService.GetTranscriptDownloadAsync(jobId, cancellationToken);
        if (result is null)
        {
            return NotFound(new
            {
                error = "Meeting transcript not found."
            });
        }

        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("{jobId:guid}/result")]
    public async Task<IActionResult> GetResult(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await _meetingMinutesResultService.GetMinutesAsync(jobId, cancellationToken);
        if (result is null)
        {
            return NotFound(new
            {
                error = "Meeting minutes not found."
            });
        }

        return Ok(new
        {
            jobId = result.JobId,
            title = result.Title,
            summary = result.Summary,
            attendees = result.Attendees,
            discussionPoints = result.DiscussionPoints,
            decisions = result.Decisions,
            actionItems = result.ActionItems,
            risks = result.Risks,
            nextSteps = result.NextSteps
        });
    }

    [HttpGet("{jobId:guid}/minutes/download")]
    public async Task<IActionResult> DownloadMinutes(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await _meetingMinutesResultService.GetMinutesDownloadAsync(jobId, cancellationToken);
        if (result is null)
        {
            return NotFound(new
            {
                error = "Meeting minutes not found."
            });
        }

        return File(result.Content, result.ContentType, result.FileName);
    }
}

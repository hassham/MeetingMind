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

    public MeetingsController(
        IUploadMeetingService uploadMeetingService,
        IMeetingStatusService meetingStatusService)
    {
        _uploadMeetingService = uploadMeetingService;
        _meetingStatusService = meetingStatusService;
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
}

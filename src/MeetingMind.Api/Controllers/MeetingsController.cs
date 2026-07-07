using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Meetings;
using Microsoft.AspNetCore.Mvc;

namespace MeetingMind.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeetingsController : ControllerBase
{
    private readonly IUploadMeetingService _uploadMeetingService;

    public MeetingsController(IUploadMeetingService uploadMeetingService)
    {
        _uploadMeetingService = uploadMeetingService;
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
}

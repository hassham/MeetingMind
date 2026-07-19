using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Failures;
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
    private readonly IMeetingRetryService _meetingRetryService;
    private readonly IMeetingHistoryService _meetingHistoryService;

    public MeetingsController(
        IUploadMeetingService uploadMeetingService,
        IMeetingStatusService meetingStatusService,
        IMeetingTranscriptService meetingTranscriptService,
        IMeetingMinutesResultService meetingMinutesResultService,
        IMeetingRetryService meetingRetryService,
        IMeetingHistoryService meetingHistoryService)
    {
        _uploadMeetingService = uploadMeetingService;
        _meetingStatusService = meetingStatusService;
        _meetingTranscriptService = meetingTranscriptService;
        _meetingMinutesResultService = meetingMinutesResultService;
        _meetingRetryService = meetingRetryService;
        _meetingHistoryService = meetingHistoryService;
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
                errorCode = MeetingErrorCodes.UploadValidation,
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
            errorCode = result.ErrorCode,
            errorMessage = result.ErrorMessage,
            automaticRetryCount = result.AutomaticRetryCount,
            automaticRetryLimit = result.AutomaticRetryLimit,
            nextRetryAt = result.NextRetryAt,
            processingDurationSeconds = result.ProcessingDurationSeconds,
            totalDurationSeconds = result.TotalDurationSeconds
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _meetingHistoryService.GetHistoryAsync(skip, take, cancellationToken);

        return Ok(new
        {
            skip = result.Skip,
            take = result.Take,
            total = result.Total,
            items = result.Items.Select(item => new
            {
                jobId = item.JobId,
                originalFileName = item.OriginalFileName,
                status = item.Status,
                stage = item.Stage,
                progress = item.Progress,
                errorCode = item.ErrorCode,
                errorMessage = item.ErrorMessage,
                automaticRetryCount = item.AutomaticRetryCount,
                automaticRetryLimit = item.AutomaticRetryLimit,
                nextRetryAt = item.NextRetryAt,
                createdAt = item.CreatedAt,
                updatedAt = item.UpdatedAt,
                startedAt = item.StartedAt,
                completedAt = item.CompletedAt,
                processingDurationSeconds = item.ProcessingDurationSeconds,
                totalDurationSeconds = item.TotalDurationSeconds
            })
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

    [HttpPost("{jobId:guid}/retry")]
    public async Task<IActionResult> Retry(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await _meetingRetryService.RetryAsync(jobId, cancellationToken);
        if (result.FailureReason == MeetingRetryFailureReason.NotFound)
        {
            return NotFound(new
            {
                error = "Meeting job not found."
            });
        }

        if (result.FailureReason == MeetingRetryFailureReason.NotRetryable)
        {
            return Conflict(new
            {
                error = "Only failed or cancelled meeting jobs can be retried.",
                jobId = result.JobId,
                status = result.Status,
                stage = result.Stage
            });
        }

        return Accepted($"/api/meetings/{result.JobId}/status", new
        {
            jobId = result.JobId,
            status = result.Status,
            stage = result.Stage
        });
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

using MeetingMind.Application.Actions;
using MeetingMind.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MeetingMind.Api.Controllers;

[ApiController]
[Route("api/actions")]
public sealed class ActionsController : ControllerBase
{
    private readonly IActionService _service;
    public ActionsController(IActionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ActionListResult>> List([FromQuery] int skip = 0, [FromQuery] int take = 25, [FromQuery] ActionItemStatus? status = null, [FromQuery] string? assignee = null, [FromQuery] string? due = null, [FromQuery] ActionItemSource? source = null, [FromQuery] Guid? meetingId = null, CancellationToken ct = default)
        => Ok(await _service.ListAsync(new(skip, take, status, assignee, due, source, meetingId), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActionItemView>> Get(Guid id, CancellationToken ct) => Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ActionItemView>> Create(CreateActionRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ActionItemView>> Update(Guid id, UpdateActionRequest request, CancellationToken ct) => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _service.DeleteAsync(id, ct); return NoContent(); }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string format, [FromQuery] Guid[]? ids, [FromQuery] ActionItemStatus? status = null, [FromQuery] string? assignee = null, [FromQuery] string? due = null, [FromQuery] ActionItemSource? source = null, [FromQuery] Guid? meetingId = null, CancellationToken ct = default)
    {
        var file = await _service.ExportAsync(new(format.ToLowerInvariant(), ids, status, assignee, due, source, meetingId), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}

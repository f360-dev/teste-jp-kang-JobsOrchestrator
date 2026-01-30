using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobsOrchestrator.Application.Commands;
using JobsOrchestrator.Application.Queries;
using MediatR;
using JobsOrchestrator.Application.Requests;

namespace JobsOrchestrator.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IMediator _mediator;

    public JobsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempHeader) || string.IsNullOrWhiteSpace(idempHeader))
            return BadRequest("Missing Idempotency-Key header");

        var cmd = new CreateJobCommand(idempHeader.ToString(), request.Payload, request.Priority, request.ScheduledAt);
        var jobId = await _mediator.Send(cmd, ct);
        return Ok(new { jobId });
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("Invalid id");

        var query = new GetJobByIdQuery(id);
        var job = await _mediator.Send(query, ct);
        
        if (job == null) return NotFound();

        return Ok(new
        {
            jobId = job.Id,
            status = job.Status.ToString(),
            priority = job.Priority.ToString(),
            payload = job.Payload,
            createdAt = job.CreatedAt,
            scheduledAt = job.ScheduledAt,
            attempts = job.Attempts,
            cancelRequested = job.CancelRequested,
            lastError = job.LastError
        });
    }

    [HttpPost("{id}/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("invalid id");

        var cmd = new CancelJobCommand(id);
        var ok = await _mediator.Send(cmd, ct);
        if (!ok) return NotFound();

        return Ok();
    }
}

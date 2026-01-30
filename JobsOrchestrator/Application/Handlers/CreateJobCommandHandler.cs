using MediatR;
using JobsOrchestrator.Application.Commands;
using JobsOrchestrator.Application.Interfaces;
using JobsOrchestrator.Domain.Models;
using System.Diagnostics;

namespace JobsOrchestrator.Application.Handlers;

public class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, string>
{
    private readonly IJobRepository _repo;

    public CreateJobCommandHandler(IJobRepository repo)
    {
        _repo = repo;
    }

    public async Task<string> Handle(CreateJobCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repo.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (existing != null) return existing.Id;

        var job = new Job
        {
            Id = Guid.NewGuid().ToString(),
            IdempotencyKey = request.IdempotencyKey,
            Payload = request.Payload,
            Priority = request.Priority switch
            {
                "High" => JobPriority.High,
                "Low" => JobPriority.Low,
                _ => JobPriority.Medium
            },
            ScheduledAt = request.ScheduledAt,
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.Pending,
            CorrelationId = Activity.Current?.Id ?? Guid.NewGuid().ToString()
        };

        await _repo.CreateJobAsync(job, cancellationToken);
        return job.Id;
    }
}

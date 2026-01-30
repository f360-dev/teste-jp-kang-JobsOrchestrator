using MediatR;

namespace JobsOrchestrator.Application.Commands;

public record CreateJobCommand(string IdempotencyKey, string Payload, string Priority, DateTime? ScheduledAt) : IRequest<string>;

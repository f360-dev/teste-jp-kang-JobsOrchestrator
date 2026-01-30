using MediatR;

namespace JobsOrchestrator.Application.Commands;

public record CancelJobCommand(string JobId) : IRequest<bool>;

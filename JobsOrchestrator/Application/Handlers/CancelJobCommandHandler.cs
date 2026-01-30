using MediatR;
using JobsOrchestrator.Application.Commands;
using JobsOrchestrator.Application.Interfaces;

namespace JobsOrchestrator.Application.Handlers;

public class CancelJobCommandHandler : IRequestHandler<CancelJobCommand, bool>
{
    private readonly IJobRepository _repo;

    public CancelJobCommandHandler(IJobRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> Handle(CancelJobCommand request, CancellationToken cancellationToken)
    {
        return await _repo.CancelJobAsync(request.JobId, cancellationToken);
    }
}

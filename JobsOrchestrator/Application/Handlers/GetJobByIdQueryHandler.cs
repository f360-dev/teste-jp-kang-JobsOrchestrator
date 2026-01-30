using JobsOrchestrator.Application.Interfaces;
using JobsOrchestrator.Application.Queries;
using JobsOrchestrator.Domain.Models;
using MediatR;

namespace JobsOrchestrator.Application.Handlers;

public class GetJobByIdQueryHandler : IRequestHandler<GetJobByIdQuery, Job?>
{
    private readonly IJobRepository _repository;

    public GetJobByIdQueryHandler(IJobRepository repository)
    {
        _repository = repository;
    }

    public Task<Job?> Handle(GetJobByIdQuery request, CancellationToken cancellationToken)
    {
        return _repository.GetByIdAsync(request.JobId, cancellationToken);
    }
}
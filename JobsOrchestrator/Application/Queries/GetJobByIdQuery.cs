using JobsOrchestrator.Domain.Models;
using MediatR;

namespace JobsOrchestrator.Application.Queries;

public record GetJobByIdQuery(string JobId) : IRequest<Job?>;
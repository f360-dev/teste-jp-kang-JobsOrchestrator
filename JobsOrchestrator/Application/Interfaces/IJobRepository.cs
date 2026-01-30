using JobsOrchestrator.Domain.Models;

namespace JobsOrchestrator.Application.Interfaces;

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(string jobId, CancellationToken ct = default);
    Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<Job> CreateJobAsync(Job job, CancellationToken ct = default);
    Task<Job?> AcquireNextJobAsync(DateTime now, CancellationToken ct = default);
    Task MarkCompletedAsync(string jobId, CancellationToken ct = default);
    Task<bool> CancelJobAsync(string jobId, CancellationToken ct = default);
    Task MarkFailedAsync(string jobId, string error, CancellationToken ct = default);
    Task AddToDeadLetterAsync(Job job, string reason, CancellationToken ct = default); 
}

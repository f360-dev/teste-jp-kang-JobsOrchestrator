using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using JobsOrchestrator.Application.Interfaces;
using JobsOrchestrator.Domain.Models;
using Polly;
using Polly.CircuitBreaker;
using Serilog.Context;

namespace JobsOrchestrator.Workers;

public class JobWorkerHostedService : BackgroundService
{
    private readonly ILogger<JobWorkerHostedService> _log;
    private readonly IServiceProvider _services;
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;

    public JobWorkerHostedService(ILogger<JobWorkerHostedService> log, IServiceProvider services)
    {
        _log = log;
        _services = services;
        _circuitBreaker = Policy.Handle<Exception>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30),
                onBreak: (ex, ts) => _log.LogWarning("Circuit broken: {reason}", ex.Message),
                onReset: () => _log.LogInformation("Circuit reset"),
                onHalfOpen: () => _log.LogInformation("Circuit half-open"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("JobWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            Job? job = null;
            try
            {
                using var scope = _services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();

                var now = DateTime.UtcNow;
                job = await repo.AcquireNextJobAsync(now, stoppingToken);
                if (job == null)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                using (LogContext.PushProperty("CorrelationId", job.CorrelationId))
                {
                    _log.LogInformation("Processing job {JobId} priority {Priority}", job.Id, job.Priority);

                    await _circuitBreaker.ExecuteAsync(async () =>
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            if (job.CancelRequested)
                            {
                                _log.LogInformation("Job {JobId} cancel requested; marking cancelled", job.Id);
                                await repo.CancelJobAsync(job.Id);
                                return;
                            }
                            await Task.Delay(200, stoppingToken);
                        }
                    });

                    if (!job.CancelRequested)
                    {
                        await repo.MarkCompletedAsync(job.Id, stoppingToken);
                        _log.LogInformation("Job {JobId} completed successfully", job.Id);
                    }
                }
            }
            catch (BrokenCircuitException ex)
            {
                if (job != null)
                {
                    using var scope = _services.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
                    await repo.MarkFailedAsync(job.Id, $"Circuit breaker open: {ex.Message}", stoppingToken);
                }
                _log.LogError(ex, "Circuit breaker triggered for job {JobId}", job?.Id);
                await Task.Delay(5000, stoppingToken); 
            }
            catch (Exception ex)
            {
                if (job != null)
                {
                    using var scope = _services.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
                    await repo.MarkFailedAsync(job.Id, ex.Message, stoppingToken);
                    _log.LogError(ex, "Job {JobId} processing failed (attempt {Attempts})", job.Id, job.Attempts);
                }
                else
                {
                    _log.LogError(ex, "Worker error without active job");
                }
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}

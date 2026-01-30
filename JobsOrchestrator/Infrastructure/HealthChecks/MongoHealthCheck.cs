using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace JobsOrchestrator.Infrastructure.HealthChecks;

public class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoClient _client;

    public MongoHealthCheck(IMongoClient client) => _client = client;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.ListDatabaseNamesAsync(cancellationToken);
            return HealthCheckResult.Healthy("MongoDB is responsive");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB is unreachable", ex);
        }
    }
}
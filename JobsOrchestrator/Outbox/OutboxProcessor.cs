using MongoDB.Driver;
using Microsoft.Extensions.Options;
using JobsOrchestrator.Infrastructure.Configuration;
using MassTransit;
using JobsOrchestrator.Application.Messages;

namespace JobsOrchestrator.Outbox;

public class OutboxProcessor : BackgroundService
{
    private readonly ILogger<OutboxProcessor> _log;
    private readonly IMongoCollection<OutboxMessage> _outbox;
    private readonly IServiceScopeFactory _scopeFactory;
    private int _retryCount = 0;

    public OutboxProcessor(
        IMongoClient client,
        IOptions<MongoDbSettings> mongoOptions,
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> log)       
    {
        _log = log;
        _scopeFactory = scopeFactory;
        var db = client.GetDatabase(mongoOptions.Value.DatabaseName);   
        _outbox = db.GetCollection<OutboxMessage>(mongoOptions.Value.OutboxCollection);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("OutboxProcessor started with batch processing");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batchSize = 100;
                var filter = Builders<OutboxMessage>.Filter.Eq(m => m.Sent, false);
                var messages = await _outbox.Find(filter).Limit(batchSize).ToListAsync(stoppingToken);
                
                if (messages.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                var tasks = messages.Select(msg => ProcessMessageAsync(msg, stoppingToken));
                await Task.WhenAll(tasks);
                
                _log.LogInformation("Processed batch of {Count} messages", messages.Count);
                
                _retryCount = 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Batch processing failure (retry {RetryCount})", _retryCount);
                
                var delaySec = Math.Min(60, Math.Pow(2, _retryCount));
                await Task.Delay(TimeSpan.FromSeconds(delaySec), stoppingToken);
                
                _retryCount++;
            }
        }
    }

    private async Task ProcessMessageAsync(OutboxMessage msg, CancellationToken cancellationToken)
    {
        try
        {
            // Atomic claim with lock
            var lockToken = Guid.NewGuid().ToString();
            var filter = Builders<OutboxMessage>.Filter.And(
                Builders<OutboxMessage>.Filter.Eq(m => m.Id, msg.Id),
                Builders<OutboxMessage>.Filter.Eq(m => m.Sent, false),
                Builders<OutboxMessage>.Filter.Or(
                    Builders<OutboxMessage>.Filter.Eq(m => m.LockedUntil, null),
                    Builders<OutboxMessage>.Filter.Lt(m => m.LockedUntil, DateTime.UtcNow)
                )
            );
            
            var update = Builders<OutboxMessage>.Update
                .Set(m => m.ProcessingToken, lockToken)
                .Set(m => m.LockedUntil, DateTime.UtcNow.AddMinutes(1));
            
            var claimed = await _outbox.FindOneAndUpdateAsync(filter, update, 
                new FindOneAndUpdateOptions<OutboxMessage> { ReturnDocument = ReturnDocument.After }, 
                cancellationToken);
            
            if (claimed == null) return; // Another instance claimed it

            var messageObject = System.Text.Json.JsonSerializer.Deserialize<JobCreatedEvent>(claimed.Body);
            if (messageObject == null)
            {
                await MarkAsFailedAsync(claimed.Id, "Deserialization failed", cancellationToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            
            await publishEndpoint.Publish(messageObject, context =>
            {
                context.MessageId = Guid.NewGuid();
                context.CorrelationId = Guid.Parse(claimed.Id);
            }, cancellationToken);

            _log.LogInformation("Published message {MessageId} via MassTransit", claimed.Id);

            var finalUpdate = Builders<OutboxMessage>.Update
                .Set(m => m.Sent, true)
                .Set(m => m.SentAt, DateTime.UtcNow);
                
            await _outbox.UpdateOneAsync(
                Builders<OutboxMessage>.Filter.And(
                    Builders<OutboxMessage>.Filter.Eq(m => m.Id, claimed.Id),
                    Builders<OutboxMessage>.Filter.Eq(m => m.ProcessingToken, lockToken)
                ), 
                finalUpdate, 
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to process message {MessageId}", msg.Id);
            throw;
        }
    }

    private async Task MarkAsFailedAsync(string messageId, string error, CancellationToken ct)
    {
        var update = Builders<OutboxMessage>.Update
            .Set(m => m.Sent, true)
            .Set(m => m.LastError, error);
        await _outbox.UpdateOneAsync(m => m.Id == messageId, update, cancellationToken: ct);
    }
}


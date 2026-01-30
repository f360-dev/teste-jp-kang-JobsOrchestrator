using JobsOrchestrator.Application.Interfaces;
using JobsOrchestrator.Application.Messages;
using JobsOrchestrator.Domain.Models;
using JobsOrchestrator.Infrastructure.Configuration;
using JobsOrchestrator.Outbox;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JobsOrchestrator.Infrastructure.Repositories;

public class JobRepository : IJobRepository
{
    private readonly IMongoCollection<Job> _jobs;
    private readonly IMongoCollection<OutboxMessage> _outbox;
    private readonly IMongoCollection<Job> _deadLetter;
    private readonly IMongoClient _client;
    private readonly MongoDbSettings _settings;

    public JobRepository(IMongoClient client, IOptions<MongoDbSettings> options)
    {
        _client = client;
        _settings = options.Value;
        var db = _client.GetDatabase(_settings.DatabaseName);
        _jobs = db.GetCollection<Job>(_settings.JobsCollection);
        _outbox = db.GetCollection<OutboxMessage>(_settings.OutboxCollection);
        _deadLetter = db.GetCollection<Job>(_settings.DeadLetterCollection);

        // Indexes for efficient acquisition
        var idxBuilder = Builders<Job>.IndexKeys;
        _jobs.Indexes.CreateOne(new CreateIndexModel<Job>(idxBuilder.Ascending(j => j.Status).Descending(j => j.Priority).Ascending(j => j.ScheduledAt)));
        _jobs.Indexes.CreateOne(new CreateIndexModel<Job>(idxBuilder.Ascending(j => j.IdempotencyKey)));
    }

    public async Task<Job?> GetByIdAsync(string jobId, CancellationToken ct = default)
    {
        return await _jobs.Find(j => j.Id == jobId).FirstOrDefaultAsync(ct);
    }

    public async Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        return await _jobs.Find(j => j.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(ct);
    }

    public async Task<Job> CreateJobAsync(Job job, CancellationToken ct = default)
    {
        // Outbox pattern: insert job and outbox in a single transaction
        var outbox = new OutboxMessage
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Body = System.Text.Json.JsonSerializer.Serialize(new JobCreatedEvent { JobId = job.Id, CreatedAt = job.CreatedAt }),
            Sent = false,
            CreatedAt = DateTime.UtcNow
        };

        using var session = await _client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        try
        {
            await _jobs.InsertOneAsync(session, job, cancellationToken: ct);
            await _outbox.InsertOneAsync(session, outbox, cancellationToken: ct);
            await session.CommitTransactionAsync(ct);
            return job;
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    public async Task<Job?> AcquireNextJobAsync(DateTime now, CancellationToken ct = default)
    {
        var filter = Builders<Job>.Filter.And(
            Builders<Job>.Filter.Eq(j => j.Status, JobStatus.Pending),
            Builders<Job>.Filter.Or(
                Builders<Job>.Filter.Eq(j => j.ScheduledAt, null),
                Builders<Job>.Filter.Lte(j => j.ScheduledAt, now)
            ),
            Builders<Job>.Filter.Or(
                Builders<Job>.Filter.Eq(j => j.LockedUntil, null),
                Builders<Job>.Filter.Lt(j => j.LockedUntil, now)
            )
        );

        var update = Builders<Job>.Update
            .Set(j => j.Status, JobStatus.Processing)
            .Set(j => j.LockedUntil, DateTime.UtcNow.AddMinutes(5))
            .Inc(j => j.Attempts, 1)
            .Set(j => j.LockToken, ObjectId.GenerateNewId().ToString());

        var options = new FindOneAndUpdateOptions<Job>
        {
            ReturnDocument = ReturnDocument.After,
            Sort = Builders<Job>.Sort.Descending(j => j.Priority).Ascending(j => j.CreatedAt)
        };

        return await _jobs.FindOneAndUpdateAsync(filter, update, options, ct);
    }

    public async Task MarkCompletedAsync(string jobId, CancellationToken ct = default)
    {
        var update = Builders<Job>.Update.Set(j => j.Status, JobStatus.Completed).Unset(j => j.LockToken).Unset(j => j.LockedUntil);
        await _jobs.UpdateOneAsync(j => j.Id == jobId, update, cancellationToken: ct);
    }

    public async Task<bool> CancelJobAsync(string jobId, CancellationToken ct = default)
    {
        var job = await _jobs.Find(j => j.Id == jobId).FirstOrDefaultAsync(ct);
        if (job == null) return false;

        if (job.Status == JobStatus.Pending)
        {
            var update = Builders<Job>.Update.Set(j => j.Status, JobStatus.Cancelled);
            await _jobs.UpdateOneAsync(j => j.Id == jobId, update, cancellationToken: ct);
            return true;
        }
        else if (job.Status == JobStatus.Processing)
        {
            var update = Builders<Job>.Update
                .Set(j => j.CancelRequested, true)
                .Set(j => j.Status, JobStatus.Cancelled)
                .Unset(j => j.LockToken)
                .Unset(j => j.LockedUntil);
            
            await _jobs.UpdateOneAsync(j => j.Id == jobId, update, cancellationToken: ct);
            return true;
        }

        return false;
    }

    public async Task AddToDeadLetterAsync(Job job, string reason, CancellationToken ct = default)
    {
        job.Status = JobStatus.DeadLetter;
        job.LastError = reason;
        await _deadLetter.InsertOneAsync(job, cancellationToken: ct);
        await _jobs.DeleteOneAsync(j => j.Id == job.Id, cancellationToken: ct);
    }

    public async Task MarkFailedAsync(string jobId, string error, CancellationToken ct = default)
    {
        var job = await GetByIdAsync(jobId, ct);
        if (job == null) return;

        const int MaxAttempts = 3;
        
        if (job.Attempts >= MaxAttempts)
        {
            await AddToDeadLetterAsync(job, $"Max attempts ({MaxAttempts}) exceeded. Last error: {error}", ct);
        }
        else
        {
            var update = Builders<Job>.Update
                .Set(j => j.Status, JobStatus.Pending)
                .Set(j => j.LastError, error)
                .Unset(j => j.LockToken)
                .Unset(j => j.LockedUntil);
            await _jobs.UpdateOneAsync(j => j.Id == jobId, update, cancellationToken: ct);
        }
    }

    public async Task MoveToDeadLetterAsync(string jobId, string error)
    {
        var update = Builders<Job>.Update
            .Set(j => j.Status, JobStatus.DeadLetter)
            .Set(j => j.LastError, error);
        await _jobs.UpdateOneAsync(j => j.Id == jobId, update);
    }
}

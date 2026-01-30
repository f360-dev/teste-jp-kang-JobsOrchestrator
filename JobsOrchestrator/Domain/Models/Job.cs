namespace JobsOrchestrator.Domain.Models;

public enum JobPriority { Low = 0, Medium = 1, High = 2 }
public enum JobStatus { Pending, Processing, Completed, Failed, Cancelled, DeadLetter }

public class Job
{
    // Using string id (GUID) to avoid external dependencies in this simplified implementation
    public string Id { get; set; } = null!;

    public string IdempotencyKey { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public JobPriority Priority { get; set; } = JobPriority.Medium;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ScheduledAt { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public bool CancelRequested { get; set; } = false;

    public int Attempts { get; set; } = 0;

    public DateTime? LockedUntil { get; set; }

    public string? LockToken { get; set; }

    public string? LastError { get; set; }

    public string? CorrelationId { get; set; }  // NEW
}

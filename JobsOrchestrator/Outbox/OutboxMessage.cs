namespace JobsOrchestrator.Outbox;

public class OutboxMessage
{
    public string Id { get; set; } = null!;
    public string Body { get; set; } = null!;
    public bool Sent { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string Destination => "JobCreated";
    public string? LastError { get; set; }
    public string? ProcessingToken { get; set; }
    public DateTime? LockedUntil { get; set; }
}

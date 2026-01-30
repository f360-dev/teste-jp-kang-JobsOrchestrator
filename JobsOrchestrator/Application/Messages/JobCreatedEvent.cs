namespace JobsOrchestrator.Application.Messages;

public record JobCreatedEvent
{
    public string JobId { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
}
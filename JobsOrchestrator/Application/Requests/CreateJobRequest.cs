namespace JobsOrchestrator.Application.Requests;

public class CreateJobRequest
{
    public string Payload { get; set; } = null!;
    public string Priority { get; set; } = "Medium";
    public DateTime? ScheduledAt { get; set; }
}

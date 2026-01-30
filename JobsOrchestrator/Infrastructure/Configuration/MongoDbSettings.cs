namespace JobsOrchestrator.Infrastructure.Configuration;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "jobsdb";
    public string JobsCollection { get; set; } = "jobs";
    public string OutboxCollection { get; set; } = "outbox";
    public string DeadLetterCollection { get; set; } = "deadletters";
}

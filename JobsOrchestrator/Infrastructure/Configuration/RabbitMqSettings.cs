namespace JobsOrchestrator.Infrastructure.Configuration;

public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string Exchange { get; set; } = "jobs-exchange";
}

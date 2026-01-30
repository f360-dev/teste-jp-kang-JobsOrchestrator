using FluentAssertions;
using Moq;
using JobsOrchestrator.Application.Commands;
using JobsOrchestrator.Application.Handlers;
using JobsOrchestrator.Application.Interfaces;
using JobsOrchestrator.Domain.Models;

namespace JobsOrchestrator.Tests.Application.Handlers;

public class CreateJobCommandHandlerTests
{
    private readonly Mock<IJobRepository> _mockRepository;
    private readonly CreateJobCommandHandler _sut;

    public CreateJobCommandHandlerTests()
    {
        _mockRepository = new Mock<IJobRepository>();
        _sut = new CreateJobCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WhenIdempotencyKeyExists_ShouldReturnExistingJobId()
    {
        // Arrange
        var idempotencyKey = "test-key-123";
        var existingJob = new Job
        {
            Id = "existing-id",
            IdempotencyKey = idempotencyKey,
            Payload = "test-payload",
            Priority = JobPriority.Medium
        };

        _mockRepository
            .Setup(x => x.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingJob);

        var command = new CreateJobCommand(idempotencyKey, "new-payload", "High", null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(existingJob.Id);
        _mockRepository.Verify(x => x.CreateJobAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenIdempotencyKeyDoesNotExist_ShouldCreateNewJob()
    {
        // Arrange
        var idempotencyKey = "new-key-456";
        var payload = "test-payload";

        _mockRepository
            .Setup(x => x.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        _mockRepository
            .Setup(x => x.CreateJobAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job job, CancellationToken ct) => job);

        var command = new CreateJobCommand(idempotencyKey, payload, "High", DateTime.UtcNow.AddHours(1));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        _mockRepository.Verify(x => x.CreateJobAsync(
            It.Is<Job>(j => 
                j.IdempotencyKey == idempotencyKey &&
                j.Payload == payload &&
                j.Priority == JobPriority.High),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("High", JobPriority.High)]
    [InlineData("Low", JobPriority.Low)]
    [InlineData("Medium", JobPriority.Medium)]
    [InlineData("Unknown", JobPriority.Medium)]
    public async Task Handle_ShouldMapPriorityCorrectly(string priorityString, JobPriority expectedPriority)
    {
        // Arrange
        _mockRepository
            .Setup(x => x.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        Job? capturedJob = null;
        _mockRepository
            .Setup(x => x.CreateJobAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Callback<Job, CancellationToken>((job, ct) => capturedJob = job)
            .ReturnsAsync((Job job, CancellationToken ct) => job);

        var command = new CreateJobCommand("key", "payload", priorityString, null);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        capturedJob.Should().NotBeNull();
        capturedJob!.Priority.Should().Be(expectedPriority);
    }
}
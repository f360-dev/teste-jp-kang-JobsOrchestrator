using FluentValidation.TestHelper;
using JobsOrchestrator.Application.Requests;
using JobsOrchestrator.Application.Validators;

namespace JobsOrchestrator.Tests.Application.Validators;

public class CreateJobRequestValidatorTests
{
    private readonly CreateJobRequestValidator _validator;

    public CreateJobRequestValidatorTests()
    {
        _validator = new CreateJobRequestValidator();
    }

    [Fact]
    public void Validate_WithValidRequest_ShouldNotHaveErrors()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            Payload = "valid-payload",
            Priority = "High"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPayload_ShouldHaveError()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            Payload = "",
            Priority = "Medium"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Payload);
    }

    [Fact]
    public void Validate_WithPayloadTooLong_ShouldHaveError()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            Payload = new string('x', 20_001),
            Priority = "Low"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Payload);
    }

    [Theory]
    [InlineData("High")]
    [InlineData("Medium")]
    [InlineData("Low")]
    public void Validate_WithValidPriority_ShouldNotHaveError(string priority)
    {
        // Arrange
        var request = new CreateJobRequest
        {
            Payload = "test",
            Priority = priority
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("CRITICAL")]
    [InlineData("")]
    public void Validate_WithInvalidPriority_ShouldHaveError(string priority)
    {
        // Arrange
        var request = new CreateJobRequest
        {
            Payload = "test",
            Priority = priority
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Priority);
    }
}
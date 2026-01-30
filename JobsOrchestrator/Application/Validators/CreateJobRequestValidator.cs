using FluentValidation;
using JobsOrchestrator.Application.Requests;

namespace JobsOrchestrator.Application.Validators;

public class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(x => x.Payload).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.Priority).Must(p => new[] { "Low", "Medium", "High" }.Contains(p))
            .WithMessage("Priority must be Low, Medium or High");
    }
}

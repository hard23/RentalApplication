using FluentValidation;

namespace HB.RentalApplication.Application.Commands.CreateApplication;

public class CreateApplicationCommandValidator : AbstractValidator<CreateApplicationCommand>
{
    public CreateApplicationCommandValidator()
    {
        RuleFor(x => x.PropertyId)
            .NotEmpty().WithMessage("PropertyId must not be empty.")
            .NotEqual(Guid.Empty).WithMessage("PropertyId must not be Guid.Empty.");

        RuleFor(x => x.RenterEmail)
            .NotEmpty().WithMessage("RenterEmail is required.")
            .EmailAddress().WithMessage("RenterEmail must be a valid email address.");

        RuleFor(x => x.HouseholdSize)
            .InclusiveBetween(1, 12).WithMessage("HouseholdSize must be between 1 and 12.");
    }
}

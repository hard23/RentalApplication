using FluentValidation;
using HB.RentalApplication.Domain;

namespace HB.RentalApplication.Api.Contracts;

/// <summary>HTTP request body for POST /applications. Carries fields the Application command does not yet support.</summary>
public sealed class CreateApplicationRequest
{
    public Guid PropertyId { get; set; }
    public string RenterEmail { get; set; } = string.Empty;
    public int HouseholdSize { get; set; }
    public decimal AnnualIncome { get; set; }
}

/// <summary>HTTP request body for POST /applications/{id}/transitions. ApplicationId comes from the route.</summary>
public sealed class TransitionApplicationRequest
{
    public RentalApplicationStatus To { get; set; }
    public string? Reason { get; set; }
    public string? Actor { get; set; }
}

/// <summary>Query string for GET /applications. Bound with [AsParameters].</summary>
public sealed class GetApplicationsRequest
{
    public Guid? PropertyId { get; set; }
    public RentalApplicationStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class CreateApplicationRequestValidator : AbstractValidator<CreateApplicationRequest>
{
    public CreateApplicationRequestValidator()
    {
        RuleFor(x => x.HouseholdSize)
            .InclusiveBetween(1, 12).WithMessage("HouseholdSize must be between 1 and 12.");

        RuleFor(x => x.AnnualIncome)
            .GreaterThanOrEqualTo(0).WithMessage("AnnualIncome must be zero or greater.");
    }
}

public sealed class TransitionApplicationRequestValidator : AbstractValidator<TransitionApplicationRequest>
{
    public TransitionApplicationRequestValidator()
    {
        RuleFor(x => x.To)
            .IsInEnum().WithMessage("to must be a valid application status.")
            .OverridePropertyName("to");

        RuleFor(x => x.Actor)
            .Must(actor => !string.IsNullOrWhiteSpace(actor)).WithMessage("actor is required.")
            .Must(actor => actor is null || actor.Trim().ToLowerInvariant() is "owner" or "renter")
            .WithMessage("actor must be 'owner' or 'renter'.")
            .OverridePropertyName("actor");
    }
}

public sealed class GetApplicationsRequestValidator : AbstractValidator<GetApplicationsRequest>
{
    public GetApplicationsRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("page must be greater than or equal to 1.")
            .OverridePropertyName("page");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("pageSize must be between 1 and 100.")
            .OverridePropertyName("pageSize");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("status must be a valid application status.")
            .When(x => x.Status.HasValue)
            .OverridePropertyName("status");
    }
}

using FluentValidation;
using FluentValidation.Results;
using HB.RentalApplication.Api.Contracts;
using HB.RentalApplication.Application.Commands.CreateApplication;
using HB.RentalApplication.Application.Commands.TransitionApplication;
using HB.RentalApplication.Application.Queries.GetApplicationById;
using HB.RentalApplication.Application.Queries.GetApplicationHistory;
using HB.RentalApplication.Application.Queries.GetApplications;
using HB.RentalApplication.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HB.RentalApplication.Api.Controllers;

[ApiController]
[Route("applications")]
[Produces("application/json")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ApplicationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // POST /applications
    [HttpPost]
    [ProducesResponseType(typeof(CreateApplicationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateApplication(
        [FromBody] CreateApplicationRequest request,
        [FromServices] IValidator<CreateApplicationRequest> requestValidator,
        [FromServices] IValidator<CreateApplicationCommand> commandValidator,
        CancellationToken cancellationToken)
    {
        var command = new CreateApplicationCommand
        {
            PropertyId = request.PropertyId,
            RenterEmail = request.RenterEmail,
            AnnualIncome = request.AnnualIncome,
            HouseholdSize = request.HouseholdSize
        };

        var errors = new List<ValidationFailure>();
        errors.AddRange((await requestValidator.ValidateAsync(request, cancellationToken)).Errors);
        errors.AddRange((await commandValidator.ValidateAsync(command, cancellationToken)).Errors);

        if (errors.Count > 0)
            return ValidationProblem(new ValidationProblemDetails(ToErrorDictionary(errors)));

        var response = await _mediator.Send(command, cancellationToken);

        return Created($"/applications/{response.ApplicationId}", response);
    }

    // GET /applications/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetApplicationByIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplicationById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetApplicationByIdQuery(id), cancellationToken);
        return Ok(response);
    }

    // GET /applications?propertyId=&status=&page=&pageSize=
    [HttpGet]
    [ProducesResponseType(typeof(GetApplicationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetApplications(
        [FromQuery] GetApplicationsRequest request,
        [FromServices] IValidator<GetApplicationsRequest> validator,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateOrNull(validator, request, cancellationToken);
        if (invalid is not null)
            return invalid;

        var response = await _mediator.Send(new GetApplicationsQuery
        {
            PropertyId = request.PropertyId,
            Status = request.Status,
            Page = request.Page,
            PageSize = request.PageSize
        }, cancellationToken);

        return Ok(response);
    }

    // POST /applications/{id}/transitions
    // NOTE: Actor is taken from the request body and is NOT authenticated. No authentication/authorization
    // is configured in this API yet; add it before production use and derive the actor from the identity.
    [HttpPost("{id:guid}/transitions")]
    [ProducesResponseType(typeof(TransitionApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TransitionApplication(
        Guid id,
        [FromBody] TransitionApplicationRequest request,
        [FromServices] IValidator<TransitionApplicationRequest> validator,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateOrNull(validator, request, cancellationToken);
        if (invalid is not null)
            return invalid;

        var response = await _mediator.Send(new TransitionApplicationCommand
        {
            ApplicationId = id,
            To = request.To,
            Reason = request.Reason,
            Actor = request.Actor!
        }, cancellationToken);

        return Ok(response);
    }

    // GET /applications/{id}/history
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(GetApplicationHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplicationHistory(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetApplicationHistoryQuery(id), cancellationToken);
        return Ok(response);
    }

    private async Task<IActionResult?> ValidateOrNull<T>(IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        return result.IsValid
            ? null
            : ValidationProblem(new ValidationProblemDetails(ToErrorDictionary(result.Errors)));
    }

    private static IDictionary<string, string[]> ToErrorDictionary(IEnumerable<ValidationFailure> failures)
        => failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
}

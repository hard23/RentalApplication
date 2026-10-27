using MediatR;
using HB.RentalApplication.Domain.Repositories;

namespace HB.RentalApplication.Application.Queries.GetApplicationById;

public class GetApplicationByIdQueryHandler : IRequestHandler<GetApplicationByIdQuery, GetApplicationByIdResponse>
{
    private readonly IRentalApplicationRepository _repository;

    public GetApplicationByIdQueryHandler(IRentalApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetApplicationByIdResponse> Handle(GetApplicationByIdQuery request, CancellationToken cancellationToken)
    {
        var application = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (application == null)
        {
            throw new KeyNotFoundException($"Rental application with ID {request.Id} not found.");
        }

        return new GetApplicationByIdResponse
        {
            Id = application.Id,
            PropertyId = application.PropertyId,
            RenterEmail = application.ApplicantEmail,
            HouseholdSize = application.HouseholdSize,
            AnnualIncome = application.AnnualIncome,
            Status = application.Status.ToString(),
            SubmittedAtUtc = application.CreatedAtUtc
        };
    }
}

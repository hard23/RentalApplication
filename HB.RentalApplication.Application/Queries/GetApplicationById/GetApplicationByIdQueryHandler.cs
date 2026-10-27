using HB.RentalApplication.Infrastructure.Persistence;
using MediatR;
using HB.RentalApplication.Application.Queries.GetApplicationById;
using System.Threading;
using System.Threading.Tasks;

namespace HB.RentalApplication.Application.Queries.GetApplicationById;

public class GetApplicationByIdQueryHandler : IRequestHandler<GetApplicationByIdQuery, GetApplicationByIdResponse>
{
    private readonly RentalApplicationRepository _repository;

    public GetApplicationByIdQueryHandler(RentalApplicationRepository repository)
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
            HouseholdSize = 0, // Not present in domain, default to 0
            AnnualIncome = 0, // Not present in domain, default to 0
            Status = application.Status.ToString(),
            SubmittedAtUtc = application.CreatedAtUtc
        };
    }
}

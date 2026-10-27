using RentalApplicationEntity = HB.RentalApplication.Domain.RentalApplication;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Infrastructure.Persistence;
using MediatR;

namespace HB.RentalApplication.Application.Commands.CreateApplication;

public class CreateApplicationCommandHandler : IRequestHandler<CreateApplicationCommand, CreateApplicationResponse>
{
    private readonly RentalApplicationRepository _repository;
    private readonly RentalApplicationHistoryRepository _historyRepository;

    public CreateApplicationCommandHandler(RentalApplicationRepository repository, RentalApplicationHistoryRepository historyRepository)
    {
        _repository = repository;
        _historyRepository = historyRepository;
    }

    public async Task<CreateApplicationResponse> Handle(CreateApplicationCommand request, CancellationToken cancellationToken)
    {
        var application = new RentalApplicationEntity
        {
            Id = Guid.NewGuid(),
            PropertyId = request.PropertyId,
            ApplicantFullName = request.RenterFullName,
            ApplicantEmail = request.RenterEmail,
            PropertyAddress = request.PropertyAddress,
            AnnualIncome = request.AnnualIncome,
            HouseholdSize = request.HouseholdSize,
            Status = RentalApplicationStatus.Submitted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsActive = true,
            IsDeleted = false
        };

        // Stage the initial history entry on the same DbContext; it is committed by the
        // repository save below together with the new application.
        await _historyRepository.AddAsync(new RentalApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            FromStatus = RentalApplicationStatus.Submitted,
            ToStatus = RentalApplicationStatus.Submitted,
            TimestampUtc = application.CreatedAtUtc,
            Actor = "renter",
            Reason = "Application created"
        }, cancellationToken);

        await _repository.AddAsync(application, cancellationToken);

        return new CreateApplicationResponse
        {
            ApplicationId = application.Id,
            Status = application.Status.ToString(),
            CreatedAtUtc = application.CreatedAtUtc
        };
    }
}

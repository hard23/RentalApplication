using HB.RentalApplication.Infrastructure.Persistence;
using MediatR;
using HB.RentalApplication.Domain;

namespace HB.RentalApplication.Application.Commands.TransitionApplication;

public class TransitionApplicationCommandHandler : IRequestHandler<TransitionApplicationCommand, TransitionApplicationResponse>
{
    private readonly RentalApplicationRepository _repository;
    private readonly RentalApplicationHistoryRepository _historyRepository;

    public TransitionApplicationCommandHandler(RentalApplicationRepository repository, RentalApplicationHistoryRepository historyRepository)
    {
        _repository = repository;
        _historyRepository = historyRepository;
    }

    public async Task<TransitionApplicationResponse> Handle(TransitionApplicationCommand request, CancellationToken cancellationToken)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (application == null)
            throw new KeyNotFoundException($"Rental application with ID {request.ApplicationId} not found.");

        // Business rules for transitions
        var current = application.Status;
        var fromStatus = current;
        var to = request.To;
        var actor = request.Actor?.ToLowerInvariant();
        bool valid = false;

        if (current == RentalApplicationStatus.Submitted && to == RentalApplicationStatus.UnderReview && actor == "owner")
        {
            valid = true;
        }
        else if (current == RentalApplicationStatus.UnderReview
            && (to == RentalApplicationStatus.Approved || to == RentalApplicationStatus.Denied)
            && actor == "owner")
        {
            valid = true;
        }
        else if ((current == RentalApplicationStatus.Submitted || current == RentalApplicationStatus.UnderReview)
            && to == RentalApplicationStatus.Withdrawn && actor == "renter")
        {
            valid = true;
        }
        else if (current == RentalApplicationStatus.Approved || current == RentalApplicationStatus.Denied || current == RentalApplicationStatus.Withdrawn)
        {
            throw new InvalidOperationException($"Cannot transition from final state '{current}'.");
        }

        if (!valid)
            throw new InvalidOperationException($"Invalid transition from '{current}' to '{to}' by '{actor}'.");

        application.Status = to;
        application.UpdatedAtUtc = DateTime.UtcNow;

        await _historyRepository.AddAsync(new RentalApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            FromStatus = fromStatus,
            ToStatus = to,
            TimestampUtc = application.UpdatedAtUtc,
            Actor = request.Actor ?? string.Empty,
            Reason = request.Reason
        }, cancellationToken);

        await _repository.UpdateAsync(application, cancellationToken);

        return new TransitionApplicationResponse
        {
            ApplicationId = application.Id,
            Status = application.Status.ToString(),
            TransitionedAtUtc = application.UpdatedAtUtc
        };
    }
}

using HB.RentalApplication.Infrastructure.Persistence;
using MediatR;

namespace HB.RentalApplication.Application.Queries.GetApplicationHistory;

public class GetApplicationHistoryQueryHandler : IRequestHandler<GetApplicationHistoryQuery, GetApplicationHistoryResponse>
{
    private readonly RentalApplicationRepository _repository;
    private readonly RentalApplicationHistoryRepository _historyRepository;

    public GetApplicationHistoryQueryHandler(RentalApplicationRepository repository, RentalApplicationHistoryRepository historyRepository)
    {
        _repository = repository;
        _historyRepository = historyRepository;
    }

    public async Task<GetApplicationHistoryResponse> Handle(GetApplicationHistoryQuery request, CancellationToken cancellationToken)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (application == null)
        {
            throw new KeyNotFoundException($"Rental application with ID {request.ApplicationId} not found.");
        }

        var history = await _historyRepository.GetByApplicationIdAsync(request.ApplicationId, cancellationToken);

        return new GetApplicationHistoryResponse
        {
            ApplicationId = application.Id,
            Entries = history
                .OrderBy(h => h.TimestampUtc)
                .Select(h => new GetApplicationHistoryEntryResponse
                {
                    FromStatus = h.FromStatus.ToString(),
                    ToStatus = h.ToStatus.ToString(),
                    TimestampUtc = h.TimestampUtc,
                    Actor = h.Actor,
                    Reason = h.Reason
                })
                .ToList()
        };
    }
}

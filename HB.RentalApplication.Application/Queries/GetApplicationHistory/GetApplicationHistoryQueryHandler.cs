using MediatR;
using HB.RentalApplication.Domain.Repositories;

namespace HB.RentalApplication.Application.Queries.GetApplicationHistory;

public class GetApplicationHistoryQueryHandler : IRequestHandler<GetApplicationHistoryQuery, GetApplicationHistoryResponse>
{
    private readonly IRentalApplicationRepository _repository;
    private readonly IRentalApplicationHistoryRepository _historyRepository;

    public GetApplicationHistoryQueryHandler(IRentalApplicationRepository repository, IRentalApplicationHistoryRepository historyRepository)
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

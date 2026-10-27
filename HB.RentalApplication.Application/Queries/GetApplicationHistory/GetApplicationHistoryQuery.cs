using MediatR;

namespace HB.RentalApplication.Application.Queries.GetApplicationHistory;

public class GetApplicationHistoryQuery : IRequest<GetApplicationHistoryResponse>
{
    public Guid ApplicationId { get; set; }

    public GetApplicationHistoryQuery(Guid applicationId)
    {
        ApplicationId = applicationId;
    }
}

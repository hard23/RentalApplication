using HB.RentalApplication.Domain;
using MediatR;

namespace HB.RentalApplication.Application.Queries.GetApplications;

public class GetApplicationsQuery : IRequest<GetApplicationsResponse>
{
    public Guid? PropertyId { get; set; }
    public RentalApplicationStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

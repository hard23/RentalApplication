using HB.RentalApplication.Domain.Repositories;
using MediatR;

namespace HB.RentalApplication.Application.Queries.GetApplications;

public class GetApplicationsQueryHandler : IRequestHandler<GetApplicationsQuery, GetApplicationsResponse>
{
    private readonly IRentalApplicationRepository _repository;

    public GetApplicationsQueryHandler(IRentalApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetApplicationsResponse> Handle(GetApplicationsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 1 : request.PageSize > 100 ? 100 : request.PageSize;

        var (applications, totalCount) = await _repository.GetPagedAsync(
            request.PropertyId, request.Status, page, pageSize, cancellationToken);

        var dtos = applications.Select(x => new ApplicationDto
        {
            Id = x.Id,
            PropertyId = x.PropertyId,
            RenterEmail = x.ApplicantEmail,
            Status = x.Status.ToString(),
            SubmittedAtUtc = x.CreatedAtUtc
        }).ToList();

        return new GetApplicationsResponse
        {
            Applications = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

using HB.RentalApplication.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HB.RentalApplication.Application.Queries.GetApplications;

public class GetApplicationsQueryHandler : IRequestHandler<GetApplicationsQuery, GetApplicationsResponse>
{
    private readonly RentalApplicationRepository _repository;
    private readonly RentalApplicationDbContext _dbContext;

    public GetApplicationsQueryHandler(RentalApplicationRepository repository, RentalApplicationDbContext dbContext)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<GetApplicationsResponse> Handle(GetApplicationsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 1 : request.PageSize > 100 ? 100 : request.PageSize;

        var query = _dbContext.RentalApplications.AsNoTracking().AsQueryable();

        if (request.PropertyId.HasValue)
        {
            query = query.Where(x => x.PropertyId == request.PropertyId.Value);
        }
        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var applications = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

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

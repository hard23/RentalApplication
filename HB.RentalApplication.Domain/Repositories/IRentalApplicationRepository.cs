using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HB.RentalApplication.Domain.Repositories;

public interface IRentalApplicationRepository : IRepository<HB.RentalApplication.Domain.RentalApplication>
{
    Task<(List<HB.RentalApplication.Domain.RentalApplication> Items, int TotalCount)> GetPagedAsync(
        Guid? propertyId,
        HB.RentalApplication.Domain.RentalApplicationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

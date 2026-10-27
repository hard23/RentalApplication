using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Domain.Repositories;
using System;
using System.Collections.Generic;

namespace HB.RentalApplication.Infrastructure.Persistence;

public class RentalApplicationHistoryRepository : IRentalApplicationHistoryRepository
{
    private readonly RentalApplicationDbContext _dbContext;

    public RentalApplicationHistoryRepository(RentalApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<RentalApplicationHistory>> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken)
        => await _dbContext.RentalApplicationHistories
            .AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .OrderBy(x => x.TimestampUtc)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Stages a history record. It is persisted when the application repository
    /// saves the same DbContext, so status change and history are committed atomically.
    /// </summary>
    public async Task AddAsync(RentalApplicationHistory entry, CancellationToken cancellationToken)
        => await _dbContext.RentalApplicationHistories.AddAsync(entry, cancellationToken);
}

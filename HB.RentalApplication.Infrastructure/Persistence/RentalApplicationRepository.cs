using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Domain.Repositories;
using System;

namespace HB.RentalApplication.Infrastructure.Persistence;

public class RentalApplicationRepository : IRentalApplicationRepository
{
    private readonly RentalApplicationDbContext _dbContext;

    public RentalApplicationRepository(RentalApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HB.RentalApplication.Domain.RentalApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await _dbContext.RentalApplications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(HB.RentalApplication.Domain.RentalApplication entity, CancellationToken cancellationToken)
    {
        await _dbContext.RentalApplications.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Saves application changes. Any history entries staged on the same DbContext
    // (via RentalApplicationHistoryRepository.AddAsync) are committed in the same transaction.
    public async Task UpdateAsync(HB.RentalApplication.Domain.RentalApplication entity, CancellationToken cancellationToken)
    {
        _dbContext.RentalApplications.Update(entity);
        try
        {
            // RowVersion is an EF concurrency token: if another request changed the row
            // since it was read, this save fails and nothing (status or history) is committed.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException(
                $"Rental application {entity.Id} was modified by another request. Please reload and retry.", ex);
        }
    }

    public async Task DeleteAsync(HB.RentalApplication.Domain.RentalApplication entity, CancellationToken cancellationToken)
    {
        _dbContext.RentalApplications.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(List<HB.RentalApplication.Domain.RentalApplication> Items, int TotalCount)> GetPagedAsync(
        Guid? propertyId,
        HB.RentalApplication.Domain.RentalApplicationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.RentalApplications.AsNoTracking().AsQueryable();

        if (propertyId.HasValue)
        {
            query = query.Where(x => x.PropertyId == propertyId.Value);
        }
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}


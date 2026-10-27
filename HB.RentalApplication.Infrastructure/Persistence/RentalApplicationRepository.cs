using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using HB.RentalApplication.Domain;
using System;

namespace HB.RentalApplication.Infrastructure.Persistence;

public class RentalApplicationRepository
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
}


using System;
using System.Threading;
using System.Threading.Tasks;

namespace HB.RentalApplication.Domain.Repositories;

/// <summary>
/// Generic repository abstraction for aggregate roots identified by a Guid.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(T entity, CancellationToken cancellationToken);
    Task UpdateAsync(T entity, CancellationToken cancellationToken);
    Task DeleteAsync(T entity, CancellationToken cancellationToken);
}

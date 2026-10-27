using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HB.RentalApplication.Domain.Repositories;

public interface IRentalApplicationHistoryRepository
{
    Task<List<RentalApplicationHistory>> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken);

    /// <summary>
    /// Stages a history record. It is persisted when the application repository
    /// saves the same unit of work, so status change and history are committed atomically.
    /// </summary>
    Task AddAsync(RentalApplicationHistory entry, CancellationToken cancellationToken);
}

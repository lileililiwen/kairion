using Kairion.Application.Abstractions;
using Kairion.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kairion.Infrastructure.Persistence;

internal sealed class EfObservationReadService : IObservationReadService
{
    private readonly KairionDbContext _db;
    public EfObservationReadService(KairionDbContext db) => _db = db;

    public async Task<IReadOnlyList<Guid>> DistinctSourceIdsInWindowAsync(
        Guid projectId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        Guid? clusterId,
        CancellationToken cancellationToken)
    {
        var query = _db.Observations
            .AsNoTracking()
            .Where(o => o.ProjectId == projectId
                        && o.ObservedUtc >= windowStartUtc
                        && o.ObservedUtc <= windowEndUtc);
        if (clusterId.HasValue)
        {
            query = query.Where(o => o.ClusterId == clusterId.Value);
        }
        var ids = await query
            .Select(o => o.SourceItemId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids;
    }

    public async Task<DateTime?> FirstObservedUtcForAsync(Guid sourceItemId, CancellationToken cancellationToken)
    {
        return await _db.Observations
            .AsNoTracking()
            .Where(o => o.SourceItemId == sourceItemId)
            .OrderBy(o => o.FirstObservedUtc)
            .Select(o => (DateTime?)o.FirstObservedUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly KairionDbContext _db;
    public EfUnitOfWork(KairionDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}

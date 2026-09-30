using Kairion.Application.Abstractions;
using Kairion.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kairion.Infrastructure.Persistence;

internal sealed class ResearchProjectRepository : IResearchProjectRepository
{
    private readonly KairionDbContext _db;
    public ResearchProjectRepository(KairionDbContext db) => _db = db;

    public Task<ResearchProject?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        _db.ResearchProjects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ResearchProject>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var query = _db.ResearchProjects.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(p => p.State == ResearchProjectState.Active);
        }
        return await query.OrderByDescending(p => p.UpdatedUtc).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(ResearchProject project, CancellationToken cancellationToken)
    {
        await _db.ResearchProjects.AddAsync(project, cancellationToken).ConfigureAwait(false);
    }

    public void Remove(ResearchProject project) => _db.ResearchProjects.Remove(project);
}

internal sealed class SourceItemRepository : ISourceItemRepository
{
    private readonly KairionDbContext _db;
    public SourceItemRepository(KairionDbContext db) => _db = db;

    public Task<SourceItem?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        _db.SourceItems.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<SourceItem?> FindByProviderIdentityAsync(Guid projectId, string providerId, string externalId, CancellationToken cancellationToken) =>
        _db.SourceItems.FirstOrDefaultAsync(
            s => s.ProjectId == projectId && s.ProviderId == providerId && s.ExternalId == externalId,
            cancellationToken);

    public Task<SourceItem?> FindByCanonicalUrlAsync(Guid projectId, string canonicalUrl, CancellationToken cancellationToken) =>
        _db.SourceItems.FirstOrDefaultAsync(
            s => s.ProjectId == projectId && s.CanonicalUrl == canonicalUrl,
            cancellationToken);

    public async Task<IReadOnlyList<SourceItem>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _db.SourceItems
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.ObservedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SourceItem item, CancellationToken cancellationToken)
    {
        await _db.SourceItems.AddAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public void Remove(SourceItem item) => _db.SourceItems.Remove(item);
}

internal sealed class ScreeningResultRepository : IScreeningResultRepository
{
    private readonly KairionDbContext _db;
    public ScreeningResultRepository(KairionDbContext db) => _db = db;

    public Task<ScreeningResult?> LatestForSourceAsync(Guid sourceItemId, CancellationToken cancellationToken) =>
        _db.ScreeningResults
            .Where(r => r.SourceItemId == sourceItemId)
            .OrderByDescending(r => r.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(ScreeningResult result, CancellationToken cancellationToken)
    {
        await _db.ScreeningResults.AddAsync(result, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DeepAnalysisRepository : IDeepAnalysisRepository
{
    private readonly KairionDbContext _db;
    public DeepAnalysisRepository(KairionDbContext db) => _db = db;

    public Task<DeepAnalysis?> LatestForSourceAsync(Guid sourceItemId, CancellationToken cancellationToken) =>
        _db.DeepAnalyses
            .Where(d => d.SourceItemId == sourceItemId)
            .OrderByDescending(d => d.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(DeepAnalysis analysis, CancellationToken cancellationToken)
    {
        await _db.DeepAnalyses.AddAsync(analysis, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class PainClusterRepository : IPainClusterRepository
{
    private readonly KairionDbContext _db;
    public PainClusterRepository(KairionDbContext db) => _db = db;

    public Task<PainCluster?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        _db.PainClusters.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PainCluster>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _db.PainClusters
            .AsNoTracking()
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Label)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(PainCluster cluster, CancellationToken cancellationToken)
    {
        await _db.PainClusters.AddAsync(cluster, cancellationToken).ConfigureAwait(false);
    }

    public void Remove(PainCluster cluster) => _db.PainClusters.Remove(cluster);
}

internal sealed class ClusterAssignmentRepository : IClusterAssignmentRepository
{
    private readonly KairionDbContext _db;
    public ClusterAssignmentRepository(KairionDbContext db) => _db = db;

    public Task<ClusterAssignment?> LatestForSourceAsync(Guid sourceItemId, CancellationToken cancellationToken) =>
        _db.ClusterAssignments
            .Where(a => a.SourceItemId == sourceItemId)
            .OrderByDescending(a => a.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ClusterAssignment>> ListForClusterAsync(Guid clusterId, CancellationToken cancellationToken)
    {
        return await _db.ClusterAssignments
            .AsNoTracking()
            .Where(a => a.ClusterId == clusterId)
            .OrderBy(a => a.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ClusterAssignment assignment, CancellationToken cancellationToken)
    {
        await _db.ClusterAssignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class HumanRevisionRepository : IHumanRevisionRepository
{
    private readonly KairionDbContext _db;
    public HumanRevisionRepository(KairionDbContext db) => _db = db;

    public async Task AddAsync(HumanRevision revision, CancellationToken cancellationToken)
    {
        await _db.HumanRevisions.AddAsync(revision, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HumanRevision>> ListForClusterAsync(Guid clusterId, CancellationToken cancellationToken)
    {
        return await _db.HumanRevisions
            .AsNoTracking()
            .Where(r => r.ClusterId == clusterId)
            .OrderByDescending(r => r.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class ObservationRepository : IObservationRepository
{
    private readonly KairionDbContext _db;
    public ObservationRepository(KairionDbContext db) => _db = db;

    public async Task AddAsync(Observation observation, CancellationToken cancellationToken)
    {
        await _db.Observations.AddAsync(observation, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateClusterAssignmentAsync(Guid sourceItemId, Guid? clusterId, CancellationToken cancellationToken)
    {
        var observations = await _db.Observations
            .Where(o => o.SourceItemId == sourceItemId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var observation in observations)
        {
            observation.AttachToCluster(clusterId ?? Guid.Empty);
        }
    }
}

internal sealed class SourceIngestionRunRepository : ISourceIngestionRunRepository
{
    private readonly KairionDbContext _db;
    public SourceIngestionRunRepository(KairionDbContext db) => _db = db;

    public async Task AddAsync(SourceIngestionRun run, CancellationToken cancellationToken)
    {
        await _db.SourceIngestionRuns.AddAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SourceIngestionRun>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _db.SourceIngestionRuns
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.RetrievedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SourceIngestionRun>> ListForRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken)
    {
        return await _db.SourceIngestionRuns
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.RunId == runId)
            .OrderBy(r => r.ProviderId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

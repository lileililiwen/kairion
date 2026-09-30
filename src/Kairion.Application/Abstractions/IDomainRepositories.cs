using Kairion.Domain;

namespace Kairion.Application.Abstractions;

public interface IResearchProjectRepository
{
    Task<ResearchProject?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResearchProject>> ListAsync(bool includeArchived, CancellationToken cancellationToken);
    Task AddAsync(ResearchProject project, CancellationToken cancellationToken);
    void Remove(ResearchProject project);
}

public interface ISourceItemRepository
{
    Task<SourceItem?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<SourceItem?> FindByProviderIdentityAsync(Guid projectId, string providerId, string externalId, CancellationToken cancellationToken);
    Task<SourceItem?> FindByCanonicalUrlAsync(Guid projectId, string canonicalUrl, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceItem>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task AddAsync(SourceItem item, CancellationToken cancellationToken);
    void Remove(SourceItem item);
}

public interface IScreeningResultRepository
{
    Task<ScreeningResult?> LatestForSourceAsync(Guid sourceItemId, CancellationToken cancellationToken);
    Task AddAsync(ScreeningResult result, CancellationToken cancellationToken);
}

public interface IDeepAnalysisRepository
{
    Task<DeepAnalysis?> LatestForSourceAsync(Guid sourceItemId, CancellationToken cancellationToken);
    Task AddAsync(DeepAnalysis analysis, CancellationToken cancellationToken);
}

public interface IPainClusterRepository
{
    Task<PainCluster?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PainCluster>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task AddAsync(PainCluster cluster, CancellationToken cancellationToken);
    void Remove(PainCluster cluster);
}

public interface IClusterAssignmentRepository
{
    Task<ClusterAssignment?> LatestForSourceAsync(Guid sourceItemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClusterAssignment>> ListForClusterAsync(Guid clusterId, CancellationToken cancellationToken);
    Task AddAsync(ClusterAssignment assignment, CancellationToken cancellationToken);
}

public interface IHumanRevisionRepository
{
    Task AddAsync(HumanRevision revision, CancellationToken cancellationToken);
    Task<IReadOnlyList<HumanRevision>> ListForClusterAsync(Guid clusterId, CancellationToken cancellationToken);
}

public interface IObservationRepository
{
    Task AddAsync(Observation observation, CancellationToken cancellationToken);
    Task UpdateClusterAssignmentAsync(Guid sourceItemId, Guid? clusterId, CancellationToken cancellationToken);
}

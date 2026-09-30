using System.Text.Json;
using Kairion.Application.Abstractions;
using Kairion.Application.Dtos;
using Kairion.Application.Schemas;
using Kairion.Domain;

namespace Kairion.Application.UseCases;

/// <summary>
/// Cluster lifecycle: create / propose assignments from a deep analysis, edit by humans,
/// merge / split, mark false positive, and list with evidence counts. Every human edit
/// records a <see cref="HumanRevision"/> row that takes precedence over later AI
/// suggestions.
/// </summary>
public sealed class ClusteringService
{
    private readonly IResearchProjectRepository _projects;
    private readonly ISourceItemRepository _sourceItems;
    private readonly IDeepAnalysisRepository _analyses;
    private readonly IPainClusterRepository _clusters;
    private readonly IClusterAssignmentRepository _assignments;
    private readonly IHumanRevisionRepository _revisions;
    private readonly IObservationRepository _observations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ClusteringService(
        IResearchProjectRepository projects,
        ISourceItemRepository sourceItems,
        IDeepAnalysisRepository analyses,
        IPainClusterRepository clusters,
        IClusterAssignmentRepository assignments,
        IHumanRevisionRepository revisions,
        IObservationRepository observations,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _projects = projects;
        _sourceItems = sourceItems;
        _analyses = analyses;
        _clusters = clusters;
        _assignments = assignments;
        _revisions = revisions;
        _observations = observations;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ClusterResponse>> EnsureClusterAsync(
        Guid projectId,
        string label,
        string category,
        string summary,
        CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null) return Result<ClusterResponse>.Failure($"Research project {projectId} not found.");

        try
        {
            var now = _clock.UtcNow;
            var cluster = new PainCluster(
                id: Guid.NewGuid(),
                projectId: projectId,
                label: label,
                category: category,
                summary: summary,
                version: 1,
                createdUtc: now);
            await _clusters.AddAsync(cluster, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<ClusterResponse>.Success(ToResponse(cluster, 0, 0m));
        }
        catch (DomainValidationException ex)
        {
            return Result<ClusterResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<ClusterAssignment>> AssignSourceAsync(
        Guid clusterId,
        Guid sourceItemId,
        AssignmentOrigin origin,
        string? reason,
        CancellationToken cancellationToken)
    {
        var cluster = await _clusters.FindAsync(clusterId, cancellationToken).ConfigureAwait(false);
        if (cluster is null) return Result<ClusterAssignment>.Failure($"Cluster {clusterId} not found.");
        var source = await _sourceItems.FindAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        if (source is null) return Result<ClusterAssignment>.Failure($"Source item {sourceItemId} not found.");
        if (source.ProjectId != cluster.ProjectId)
        {
            return Result<ClusterAssignment>.Failure("Cluster and source item belong to different projects.");
        }
        var analysis = await _analyses.LatestForSourceAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        var previous = await _assignments.LatestForSourceAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;
        var assignment = new ClusterAssignment(
            id: Guid.NewGuid(),
            clusterId: clusterId,
            sourceItemId: sourceItemId,
            deepAnalysisId: analysis?.Id,
            origin: origin,
            createdUtc: now,
            supersedesId: previous?.Id);
        await _assignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);
        await _observations.UpdateClusterAssignmentAsync(sourceItemId, clusterId, cancellationToken).ConfigureAwait(false);
        if (origin == AssignmentOrigin.Human)
        {
            await _revisions.AddAsync(new HumanRevision(
                id: Guid.NewGuid(),
                action: HumanRevisionAction.ReassignedItem,
                clusterId: clusterId,
                sourceItemId: sourceItemId,
                payloadJson: JsonSerializer.Serialize(new { reason, previousClusterId = previous?.ClusterId }),
                createdUtc: now), cancellationToken).ConfigureAwait(false);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<ClusterAssignment>.Success(assignment);
    }

    public async Task<IReadOnlyList<ClusterResponse>> ListForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var clusters = await _clusters.ListForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        var result = new List<ClusterResponse>(clusters.Count);
        foreach (var cluster in clusters)
        {
            var items = await _assignments.ListForClusterAsync(cluster.Id, cancellationToken).ConfigureAwait(false);
            var sourceIds = items.Select(i => i.SourceItemId).Distinct().ToList();
            var confidences = new List<decimal>();
            foreach (var sid in sourceIds)
            {
                var analysis = await _analyses.LatestForSourceAsync(sid, cancellationToken).ConfigureAwait(false);
                if (analysis is { Status: AnalysisStatus.Completed })
                {
                    confidences.Add(analysis.Confidence);
                }
            }
            var average = confidences.Count == 0 ? 0m : Math.Round(confidences.Average(), 4, MidpointRounding.ToEven);
            result.Add(ToResponse(cluster, sourceIds.Count, average));
        }
        return result;
    }

    public async Task<Result<ClusterEvidenceResponse>> GetEvidenceAsync(
        Guid clusterId,
        CancellationToken cancellationToken)
    {
        var cluster = await _clusters.FindAsync(clusterId, cancellationToken).ConfigureAwait(false);
        if (cluster is null) return Result<ClusterEvidenceResponse>.Failure($"Cluster {clusterId} not found.");
        var items = new List<ClusterEvidenceItem>();
        var assignments = await _assignments.ListForClusterAsync(clusterId, cancellationToken).ConfigureAwait(false);
        // Keep the most recent assignment per source item.
        var latest = assignments
            .GroupBy(a => a.SourceItemId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedUtc).First());
        foreach (var (sourceId, assignment) in latest)
        {
            var source = await _sourceItems.FindAsync(sourceId, cancellationToken).ConfigureAwait(false);
            if (source is null) continue;
            var analysis = await _analyses.LatestForSourceAsync(sourceId, cancellationToken).ConfigureAwait(false);
            items.Add(new ClusterEvidenceItem
            {
                SourceItemId = source.Id,
                CanonicalUrl = source.CanonicalUrl,
                Title = source.Title,
                Excerpt = source.Excerpt,
                PublishedUtc = source.PublishedUtc,
                ObservedUtc = source.ObservedUtc,
                Confidence = analysis?.Confidence ?? 0m,
                ProviderId = source.ProviderId,
                AssignmentOrigin = assignment.Origin,
                AssignedAtUtc = assignment.CreatedUtc,
            });
        }
        return Result<ClusterEvidenceResponse>.Success(new ClusterEvidenceResponse
        {
            ClusterId = cluster.Id,
            Label = cluster.Label,
            Category = cluster.Category,
            Summary = cluster.Summary,
            ReviewStateVersion = cluster.ReviewStateVersion,
            Items = items.OrderByDescending(i => i.Confidence).ToList(),
        });
    }

    public async Task<Result<ClusterResponse>> EditClusterAsync(
        Guid clusterId,
        UpdateClusterRequest request,
        CancellationToken cancellationToken)
    {
        var cluster = await _clusters.FindAsync(clusterId, cancellationToken).ConfigureAwait(false);
        if (cluster is null) return Result<ClusterResponse>.Failure($"Cluster {clusterId} not found.");
        try
        {
            var now = _clock.UtcNow;
            cluster.ApplyHumanEdit(request.Label, request.Category, request.Summary, now);
            await _revisions.AddAsync(new HumanRevision(
                id: Guid.NewGuid(),
                action: HumanRevisionAction.EditedCluster,
                clusterId: cluster.Id,
                sourceItemId: null,
                payloadJson: JsonSerializer.Serialize(new { request.Label, request.Category, request.Summary }),
                createdUtc: now), cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<ClusterResponse>.Success(ToResponse(cluster, 0, 0m));
        }
        catch (DomainValidationException ex)
        {
            return Result<ClusterResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<ClusterAssignment>> ReassignAsync(
        Guid clusterId,
        ReassignItemRequest request,
        CancellationToken cancellationToken)
    {
        return await AssignSourceAsync(clusterId, request.SourceItemId, AssignmentOrigin.Human, "manual_reassign", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<ClusterResponse>> MergeClustersAsync(
        Guid targetClusterId,
        MergeClustersRequest request,
        CancellationToken cancellationToken)
    {
        var target = await _clusters.FindAsync(targetClusterId, cancellationToken).ConfigureAwait(false);
        if (target is null) return Result<ClusterResponse>.Failure($"Target cluster {targetClusterId} not found.");
        var source = await _clusters.FindAsync(request.SourceClusterId, cancellationToken).ConfigureAwait(false);
        if (source is null) return Result<ClusterResponse>.Failure($"Source cluster {request.SourceClusterId} not found.");
        if (target.Id == source.Id) return Result<ClusterResponse>.Failure("Cannot merge a cluster into itself.");
        if (target.ProjectId != source.ProjectId)
        {
            return Result<ClusterResponse>.Failure("Cannot merge clusters from different projects.");
        }
        var now = _clock.UtcNow;
        var sourceAssignments = await _assignments.ListForClusterAsync(source.Id, cancellationToken).ConfigureAwait(false);
        foreach (var assignment in sourceAssignments)
        {
            var latest = await _assignments.LatestForSourceAsync(assignment.SourceItemId, cancellationToken).ConfigureAwait(false);
            if (latest is { ClusterId: var clusterId } && clusterId == source.Id)
            {
                await _assignments.AddAsync(new ClusterAssignment(
                    id: Guid.NewGuid(),
                    clusterId: target.Id,
                    sourceItemId: assignment.SourceItemId,
                    deepAnalysisId: latest.DeepAnalysisId,
                    origin: AssignmentOrigin.Human,
                    createdUtc: now,
                    supersedesId: latest.Id), cancellationToken).ConfigureAwait(false);
                await _observations.UpdateClusterAssignmentAsync(assignment.SourceItemId, target.Id, cancellationToken).ConfigureAwait(false);
            }
        }
        await _revisions.AddAsync(new HumanRevision(
            id: Guid.NewGuid(),
            action: HumanRevisionAction.MergedClusters,
            clusterId: target.Id,
            sourceItemId: null,
            payloadJson: JsonSerializer.Serialize(new { mergedFrom = source.Id }),
            createdUtc: now), cancellationToken).ConfigureAwait(false);
        _clusters.Remove(source);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<ClusterResponse>.Success(ToResponse(target, 0, 0m));
    }

    public async Task<Result<ClusterResponse>> SplitClusterAsync(
        Guid sourceClusterId,
        SplitClusterRequest request,
        CancellationToken cancellationToken)
    {
        var source = await _clusters.FindAsync(sourceClusterId, cancellationToken).ConfigureAwait(false);
        if (source is null) return Result<ClusterResponse>.Failure($"Cluster {sourceClusterId} not found.");
        try
        {
            var now = _clock.UtcNow;
            var newCluster = new PainCluster(
                id: Guid.NewGuid(),
                projectId: source.ProjectId,
                label: request.NewClusterLabel,
                category: request.NewClusterCategory,
                summary: request.NewClusterSummary,
                version: source.Version + 1,
                createdUtc: now);
            await _clusters.AddAsync(newCluster, cancellationToken).ConfigureAwait(false);
            foreach (var sourceItemId in request.SourceItemIds.Distinct())
            {
                var latest = await _assignments.LatestForSourceAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
                if (latest is null) continue;
                await _assignments.AddAsync(new ClusterAssignment(
                    id: Guid.NewGuid(),
                    clusterId: newCluster.Id,
                    sourceItemId: sourceItemId,
                    deepAnalysisId: latest.DeepAnalysisId,
                    origin: AssignmentOrigin.Human,
                    createdUtc: now,
                    supersedesId: latest.Id), cancellationToken).ConfigureAwait(false);
                await _observations.UpdateClusterAssignmentAsync(sourceItemId, newCluster.Id, cancellationToken).ConfigureAwait(false);
            }
            await _revisions.AddAsync(new HumanRevision(
                id: Guid.NewGuid(),
                action: HumanRevisionAction.SplitCluster,
                clusterId: newCluster.Id,
                sourceItemId: null,
                payloadJson: JsonSerializer.Serialize(new { fromCluster = source.Id, itemCount = request.SourceItemIds.Count }),
                createdUtc: now), cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<ClusterResponse>.Success(ToResponse(newCluster, request.SourceItemIds.Count, 0m));
        }
        catch (DomainValidationException ex)
        {
            return Result<ClusterResponse>.Failure(ex.Message);
        }
    }

    public async Task<IReadOnlyList<HumanRevisionResponse>> RevisionsForClusterAsync(
        Guid clusterId,
        CancellationToken cancellationToken)
    {
        var rows = await _revisions.ListForClusterAsync(clusterId, cancellationToken).ConfigureAwait(false);
        return rows.Select(r => new HumanRevisionResponse
        {
            Id = r.Id,
            Action = ToCamel(r.Action.ToString()),
            ClusterId = r.ClusterId,
            SourceItemId = r.SourceItemId,
            PayloadJson = r.PayloadJson,
            CreatedUtc = r.CreatedUtc,
        }).ToList();
    }

    private static ClusterResponse ToResponse(PainCluster cluster, int evidenceCount, decimal averageConfidence)
    {
        return new ClusterResponse
        {
            Id = cluster.Id,
            ProjectId = cluster.ProjectId,
            Label = cluster.Label,
            Category = cluster.Category,
            Summary = cluster.Summary,
            Version = cluster.Version,
            ReviewStateVersion = cluster.ReviewStateVersion,
            CreatedUtc = cluster.CreatedUtc,
            UpdatedUtc = cluster.UpdatedUtc,
            EvidenceCount = evidenceCount,
            AverageConfidence = averageConfidence,
        };
    }

    private static string ToCamel(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (char.IsUpper(value[0]))
        {
            return char.ToLowerInvariant(value[0]) + value[1..];
        }
        return value;
    }
}

public sealed class HumanRevisionResponse
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? ClusterId { get; set; }
    public Guid? SourceItemId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
}

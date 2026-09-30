namespace Kairion.Domain;

/// <summary>
/// A persisted first-observation timestamp for a source item within a project. Trend
/// counts are computed from these observations in application code, not supplied by AI.
/// </summary>
public sealed class Observation
{
    public Observation(
        Guid id,
        Guid projectId,
        Guid sourceItemId,
        Guid? clusterId,
        DateTime observedUtc,
        DateTime firstObservedUtc)
    {
        if (id == Guid.Empty) throw new DomainValidationException("Observation.Id is required.");
        if (projectId == Guid.Empty) throw new DomainValidationException("Observation.ProjectId is required.");
        if (sourceItemId == Guid.Empty) throw new DomainValidationException("Observation.SourceItemId is required.");
        Id = id;
        ProjectId = projectId;
        SourceItemId = sourceItemId;
        ClusterId = clusterId;
        ObservedUtc = EnsureUtc(observedUtc);
        FirstObservedUtc = EnsureUtc(firstObservedUtc);
    }

    public Guid Id { get; }
    public Guid ProjectId { get; }
    public Guid SourceItemId { get; }
    public Guid? ClusterId { get; private set; }
    public DateTime ObservedUtc { get; }
    public DateTime FirstObservedUtc { get; }

    public void AttachToCluster(Guid clusterId) => ClusterId = clusterId;

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

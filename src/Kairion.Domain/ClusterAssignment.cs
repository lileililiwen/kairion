namespace Kairion.Domain;

/// <summary>
/// Assignment of a source item (and, optionally, its deep analysis) to a pain cluster.
/// Multiple assignments can exist for the same source item across revisions; the most
/// recent non-superseded one is the active assignment.
/// </summary>
public sealed class ClusterAssignment
{
    public ClusterAssignment(
        Guid id,
        Guid clusterId,
        Guid sourceItemId,
        Guid? deepAnalysisId,
        AssignmentOrigin origin,
        DateTime createdUtc,
        Guid? supersedesId = null)
    {
        if (id == Guid.Empty) throw new DomainValidationException("ClusterAssignment.Id is required.");
        if (clusterId == Guid.Empty) throw new DomainValidationException("clusterId is required.");
        if (sourceItemId == Guid.Empty) throw new DomainValidationException("sourceItemId is required.");

        Id = id;
        ClusterId = clusterId;
        SourceItemId = sourceItemId;
        DeepAnalysisId = deepAnalysisId;
        Origin = origin;
        CreatedUtc = EnsureUtc(createdUtc);
        SupersedesId = supersedesId;
    }

    public Guid Id { get; }
    public Guid ClusterId { get; }
    public Guid SourceItemId { get; }
    public Guid? DeepAnalysisId { get; }
    public AssignmentOrigin Origin { get; }
    public DateTime CreatedUtc { get; }
    public Guid? SupersedesId { get; }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

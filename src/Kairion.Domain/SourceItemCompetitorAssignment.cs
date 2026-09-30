namespace Kairion.Domain;

/// <summary>
/// Explicit attribution of a source item to a project-scoped competitor. The
/// assignment is always explicit (owner action or an upstream typed analysis
/// step that records its origin); the competitor-gap query never infers a
/// competitor from free text. One source item may be attributed to several
/// competitors; it counts once per competitor/cluster cell.
/// </summary>
public sealed class SourceItemCompetitorAssignment
{
    public SourceItemCompetitorAssignment(
        Guid id,
        Guid projectId,
        Guid sourceItemId,
        Guid competitorId,
        AssignmentOrigin origin,
        DateTime createdUtc)
    {
        if (id == Guid.Empty) throw new DomainValidationException("Assignment Id is required.");
        if (projectId == Guid.Empty) throw new DomainValidationException("ProjectId is required.");
        if (sourceItemId == Guid.Empty) throw new DomainValidationException("SourceItemId is required.");
        if (competitorId == Guid.Empty) throw new DomainValidationException("CompetitorId is required.");

        Id = id;
        ProjectId = projectId;
        SourceItemId = sourceItemId;
        CompetitorId = competitorId;
        Origin = origin;
        CreatedUtc = EnsureUtc(createdUtc);
    }

    /// <summary>Parameterless constructor for EF Core materialization.</summary>
    private SourceItemCompetitorAssignment()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid SourceItemId { get; private set; }
    public Guid CompetitorId { get; private set; }
    public AssignmentOrigin Origin { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

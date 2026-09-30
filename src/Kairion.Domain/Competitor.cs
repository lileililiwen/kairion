namespace Kairion.Domain;

/// <summary>
/// A project-scoped competitor with a stable owner-managed identity. The
/// <see cref="Id"/> is assigned once and never reused, so evidence attributed
/// to a competitor stays linked even if the display name is edited elsewhere.
/// Names are unique per project after case-insensitive normalization.
/// </summary>
public sealed class Competitor
{
    public Competitor(Guid id, Guid projectId, string name, DateTime createdUtc)
    {
        if (id == Guid.Empty) throw new DomainValidationException("Competitor.Id is required.");
        if (projectId == Guid.Empty) throw new DomainValidationException("Competitor.ProjectId is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainValidationException("Competitor name is required.");
        var trimmed = name.Trim();
        if (trimmed.Length > 200) throw new DomainValidationException("Competitor name must be 200 characters or fewer.");

        Id = id;
        ProjectId = projectId;
        Name = trimmed;
        NormalizedName = Normalize(trimmed);
        CreatedUtc = EnsureUtc(createdUtc);
    }

    /// <summary>Parameterless constructor for EF Core materialization.</summary>
    private Competitor()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public DateTime CreatedUtc { get; private set; }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

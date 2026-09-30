namespace Kairion.Domain;

/// <summary>
/// A semantic cluster of related source items within a research project. The label and
/// summary can be edited by a human; human revisions are persisted as a separate audit
/// row and take precedence over later AI suggestions.
/// </summary>
public sealed class PainCluster
{
    public PainCluster(
        Guid id,
        Guid projectId,
        string label,
        string category,
        string summary,
        int version,
        DateTime createdUtc,
        int reviewStateVersion = 0)
    {
        if (id == Guid.Empty) throw new DomainValidationException("PainCluster.Id is required.");
        if (projectId == Guid.Empty) throw new DomainValidationException("PainCluster.ProjectId is required.");
        if (string.IsNullOrWhiteSpace(label)) throw new DomainValidationException("label is required.");
        if (string.IsNullOrWhiteSpace(category)) throw new DomainValidationException("category is required.");
        if (string.IsNullOrWhiteSpace(summary)) throw new DomainValidationException("summary is required.");

        Id = id;
        ProjectId = projectId;
        Label = label.Trim();
        Category = category.Trim();
        Summary = summary.Trim();
        Version = Math.Max(1, version);
        CreatedUtc = EnsureUtc(createdUtc);
        UpdatedUtc = CreatedUtc;
        ReviewStateVersion = Math.Max(0, reviewStateVersion);
    }

    public Guid Id { get; }
    public Guid ProjectId { get; }
    public string Label { get; private set; }
    public string Category { get; private set; }
    public string Summary { get; private set; }
    public int Version { get; }
    public int ReviewStateVersion { get; private set; }
    public DateTime CreatedUtc { get; }
    public DateTime UpdatedUtc { get; private set; }

    public void ApplyHumanEdit(string label, string category, string summary, DateTime editedUtc)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new DomainValidationException("label is required.");
        if (string.IsNullOrWhiteSpace(category)) throw new DomainValidationException("category is required.");
        if (string.IsNullOrWhiteSpace(summary)) throw new DomainValidationException("summary is required.");
        Label = label.Trim();
        Category = category.Trim();
        Summary = summary.Trim();
        UpdatedUtc = EnsureUtc(editedUtc);
        ReviewStateVersion += 1;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

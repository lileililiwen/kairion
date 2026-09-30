namespace Kairion.Domain;

/// <summary>
/// A research project, owned by the single self-hosted owner. The brief and source
/// configuration drive every downstream intake, screening, analysis, and clustering action.
/// </summary>
public sealed class ResearchProject
{
    /// <summary>
    /// Parameterless constructor for EF Core materialization. Application code MUST
    /// use the validating constructor so the brief/source invariants are enforced.
    /// </summary>
    private ResearchProject()
    {
        Title = string.Empty;
        BriefText = string.Empty;
        Topics = Array.Empty<string>();
        EnabledSourceProviderIds = Array.Empty<string>();
        IncludedCompetitors = Array.Empty<string>();
    }

    public ResearchProject(
        Guid id,
        string title,
        BriefKind briefKind,
        string briefText,
        IReadOnlyList<string> topics,
        SourceConfiguration sourceConfiguration,
        DateTime createdUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id is required.", nameof(id));
        Id = id;
        Title = ValidateRequiredText(title, nameof(title), maxLength: 200);
        BriefKind = briefKind;
        BriefText = ValidateRequiredText(briefText, nameof(briefText), maxLength: 4_000);
        Topics = NormaliseTopics(topics);
        ApplySourceConfiguration(sourceConfiguration ?? throw new ArgumentNullException(nameof(sourceConfiguration)));
        CreatedUtc = EnsureUtc(createdUtc);
        UpdatedUtc = CreatedUtc;
        State = ResearchProjectState.Active;
    }

    public Guid Id { get; }
    public string Title { get; private set; }
    public BriefKind BriefKind { get; private set; }
    public string BriefText { get; private set; }
    public IReadOnlyList<string> Topics { get; private set; }

    // SourceConfiguration fields are first-class columns on the parent table so EF
    // Core 10's InMemory provider can map them without shadow-property hydration
    // hooks. The SourceConfiguration value object is computed on demand.
    public IReadOnlyList<string> EnabledSourceProviderIds { get; private set; }
    public IReadOnlyList<string> IncludedCompetitors { get; private set; }
    public string? QueryStrategy { get; private set; }
    public DateTime? WindowStartUtc { get; private set; }
    public DateTime? WindowEndUtc { get; private set; }

    public SourceConfiguration SourceConfiguration => new(
        EnabledSourceProviderIds,
        IncludedCompetitors,
        QueryStrategy,
        WindowStartUtc,
        WindowEndUtc);

    public DateTime CreatedUtc { get; }
    public DateTime UpdatedUtc { get; private set; }
    public ResearchProjectState State { get; private set; }
    public DateTime? ArchivedUtc { get; private set; }

    public void Update(
        string title,
        BriefKind briefKind,
        string briefText,
        IReadOnlyList<string> topics,
        SourceConfiguration sourceConfiguration,
        DateTime updatedUtc)
    {
        Title = ValidateRequiredText(title, nameof(title), maxLength: 200);
        BriefKind = briefKind;
        BriefText = ValidateRequiredText(briefText, nameof(briefText), maxLength: 4_000);
        Topics = NormaliseTopics(topics);
        ApplySourceConfiguration(sourceConfiguration ?? throw new ArgumentNullException(nameof(sourceConfiguration)));
        UpdatedUtc = EnsureUtc(updatedUtc);
    }

    public void Archive(DateTime archivedUtc)
    {
        State = ResearchProjectState.Archived;
        ArchivedUtc = EnsureUtc(archivedUtc);
        UpdatedUtc = ArchivedUtc.Value;
    }

    public void Restore(DateTime restoredUtc)
    {
        State = ResearchProjectState.Active;
        ArchivedUtc = null;
        UpdatedUtc = EnsureUtc(restoredUtc);
    }

    private void ApplySourceConfiguration(SourceConfiguration configuration)
    {
        EnabledSourceProviderIds = configuration.EnabledSourceProviderIds;
        IncludedCompetitors = configuration.IncludedCompetitors;
        QueryStrategy = configuration.QueryStrategy;
        WindowStartUtc = configuration.WindowStartUtc;
        WindowEndUtc = configuration.WindowEndUtc;
    }

    private static IReadOnlyList<string> NormaliseTopics(IReadOnlyList<string>? topics) =>
        topics is null
            ? Array.Empty<string>()
            : topics
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string ValidateRequiredText(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{paramName} is required and must contain non-whitespace characters.");
        }
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException($"{paramName} must be {maxLength} characters or fewer.");
        }
        return trimmed;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

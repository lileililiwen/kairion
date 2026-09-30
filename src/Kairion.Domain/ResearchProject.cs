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
        ProviderSettingsJson = "[]";
    }

    public ResearchProject(
        Guid id,
        string title,
        BriefKind briefKind,
        string briefText,
        IReadOnlyList<string> topics,
        SourceConfiguration sourceConfiguration,
        DateTime createdUtc,
        IReadOnlyList<SourceProviderSettings>? providerSettings = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id is required.", nameof(id));
        Id = id;
        Title = ValidateRequiredText(title, nameof(title), maxLength: 200);
        BriefKind = briefKind;
        BriefText = ValidateRequiredText(briefText, nameof(briefText), maxLength: 4_000);
        Topics = NormaliseTopics(topics);
        ApplySourceConfiguration(sourceConfiguration ?? throw new ArgumentNullException(nameof(sourceConfiguration)));
        ApplyProviderSettings(providerSettings);
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

    /// <summary>
    /// Per-provider non-secret settings serialized as JSON. Omitted providers
    /// default to disabled. Secrets are never stored here.
    /// </summary>
    public string ProviderSettingsJson { get; private set; } = "[]";

    public IReadOnlyList<SourceProviderSettings> ProviderSettings => ParseProviderSettings(ProviderSettingsJson);

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
        DateTime updatedUtc,
        IReadOnlyList<SourceProviderSettings>? providerSettings = null)
    {
        Title = ValidateRequiredText(title, nameof(title), maxLength: 200);
        BriefKind = briefKind;
        BriefText = ValidateRequiredText(briefText, nameof(briefText), maxLength: 4_000);
        Topics = NormaliseTopics(topics);
        ApplySourceConfiguration(sourceConfiguration ?? throw new ArgumentNullException(nameof(sourceConfiguration)));
        ApplyProviderSettings(providerSettings);
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

    private void ApplyProviderSettings(IReadOnlyList<SourceProviderSettings>? settings)
    {
        var list = (settings ?? Array.Empty<SourceProviderSettings>())
            .GroupBy(s => s.ProviderId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(s => s.ProviderId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ProviderSettingsJson = System.Text.Json.JsonSerializer.Serialize(
            list.Select(s => new ProviderSettingsRecord(
                s.ProviderId, s.Enabled, s.MaxQueries, s.MaxResultsPerQuery, s.Endpoint, s.CredentialRef)));
        // Keep the enabled-ids column consistent with the per-provider settings:
        // a provider counts as enabled only when its settings say so.
        if (list.Count > 0)
        {
            EnabledSourceProviderIds = list.Where(s => s.Enabled).Select(s => s.ProviderId).ToArray();
        }
    }

    private static IReadOnlyList<SourceProviderSettings> ParseProviderSettings(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SourceProviderSettings>();
            var records = System.Text.Json.JsonSerializer.Deserialize<List<ProviderSettingsRecord>>(json);
            if (records is null) return Array.Empty<SourceProviderSettings>();
            return records
                .Select(r => new SourceProviderSettings(r.ProviderId, r.Enabled, r.MaxQueries, r.MaxResultsPerQuery, r.Endpoint, r.CredentialRef))
                .ToArray();
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<SourceProviderSettings>();
        }
    }

    private sealed record ProviderSettingsRecord(
        string ProviderId,
        bool Enabled,
        int MaxQueries,
        int MaxResultsPerQuery,
        string? Endpoint,
        string? CredentialRef);

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

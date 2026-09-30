namespace Kairion.Domain;

/// <summary>
/// Configured sources for a research project. The presence of a provider id enables the
/// configured adapter; absence means the project relies on manual URL intake only.
/// This is a value object: instances are immutable and the fields are stored as
/// first-class columns on the owning <see cref="ResearchProject"/>; the instance is
/// recomputed from those columns on each access.
/// </summary>
public sealed record SourceConfiguration
{
    public IReadOnlyList<string> EnabledSourceProviderIds { get; }
    public IReadOnlyList<string> IncludedCompetitors { get; }
    public string? QueryStrategy { get; }
    public DateTime? WindowStartUtc { get; }
    public DateTime? WindowEndUtc { get; }

    public SourceConfiguration(
        IReadOnlyList<string> enabledSourceProviderIds,
        IReadOnlyList<string> includedCompetitors,
        string? queryStrategy,
        DateTime? windowStartUtc,
        DateTime? windowEndUtc)
    {
        EnabledSourceProviderIds = (IReadOnlyList<string>)(enabledSourceProviderIds ?? Array.Empty<string>());
        IncludedCompetitors = (IReadOnlyList<string>)(includedCompetitors ?? Array.Empty<string>());
        QueryStrategy = string.IsNullOrWhiteSpace(queryStrategy) ? null : queryStrategy.Trim();
        WindowStartUtc = windowStartUtc?.ToUniversalTime();
        WindowEndUtc = windowEndUtc?.ToUniversalTime();
    }
}

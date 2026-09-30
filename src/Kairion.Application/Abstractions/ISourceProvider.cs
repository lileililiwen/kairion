using Kairion.Domain;

namespace Kairion.Application.Abstractions;

/// <summary>
/// A normalized source item returned from a source provider. The application layer is
/// responsible for persistence; the provider MUST NOT touch the database or know about
/// EF Core types.
/// </summary>
public sealed class SourceFetchResult
{
    public SourceFetchResult(
        string providerId,
        string externalId,
        string canonicalUrl,
        string? title,
        string? excerpt,
        DateTime? publishedUtc,
        DateTime observedUtc,
        string provenanceJson,
        ProviderObservation observation)
    {
        ProviderId = providerId;
        ExternalId = externalId;
        CanonicalUrl = canonicalUrl;
        Title = title;
        Excerpt = excerpt;
        PublishedUtc = publishedUtc?.ToUniversalTime();
        ObservedUtc = observedUtc.Kind == DateTimeKind.Utc
            ? observedUtc
            : DateTime.SpecifyKind(observedUtc, DateTimeKind.Utc);
        ProvenanceJson = provenanceJson;
        Observation = observation;
    }

    public string ProviderId { get; }
    public string ExternalId { get; }
    public string CanonicalUrl { get; }
    public string? Title { get; }
    public string? Excerpt { get; }
    public DateTime? PublishedUtc { get; }
    public DateTime ObservedUtc { get; }
    public string ProvenanceJson { get; }
    public ProviderObservation Observation { get; }
}

/// <summary>
/// Provider-neutral source provider contract. Concrete adapters (one per source) live in
/// the infrastructure layer. The contract is intentionally small to keep replacement
/// risk bounded.
/// </summary>
public interface ISourceProvider
{
    /// <summary>Stable id used in configuration and stored provenance.</summary>
    string ProviderId { get; }

    /// <summary>Human-readable provider name shown in the UI.</summary>
    string DisplayName { get; }

    /// <summary>Indicates whether the provider requires BYOK credentials.</summary>
    bool RequiresCredentials { get; }

    /// <summary>Returns true when the provider is configured and reachable.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes a search against the provider. The returned list MUST only contain
    /// candidates that the provider is permitted to expose. Provider errors, rate limits,
    /// and policy denials are encoded in the <see cref="SourceFetchResult.Observation"/>.
    /// </summary>
    Task<IReadOnlyList<SourceFetchResult>> SearchAsync(SourceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches a single candidate by its provider identity. Used to refresh metadata for
    /// an already-known source item.
    /// </summary>
    Task<SourceFetchResult?> FetchAsync(SourceReference reference, CancellationToken cancellationToken);
}

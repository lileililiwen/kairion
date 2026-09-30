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
        ProviderObservation observation,
        string? authorHandle = null)
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
        AuthorHandle = string.IsNullOrWhiteSpace(authorHandle) ? null : authorHandle.Trim();
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
    public string? AuthorHandle { get; }
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
    /// Executes a bounded search against the provider. Returns a
    /// <see cref="SourceBatch"/> with zero or more normalized candidates plus a
    /// terminal/partial status, safe diagnostic code, and retry-after hint.
    /// Provider errors, rate limits, and policy denials are encoded in the
    /// batch status — one provider failure never discards another provider's
    /// accepted candidates. The returned candidates MUST only contain items the
    /// provider is permitted to expose.
    /// </summary>
    Task<SourceBatch> SearchAsync(SourceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches a single candidate by its provider identity. Used to refresh metadata for
    /// an already-known source item.
    /// </summary>
    Task<SourceFetchResult?> FetchAsync(SourceReference reference, CancellationToken cancellationToken);
}

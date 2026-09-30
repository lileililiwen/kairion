namespace Kairion.Domain;

/// <summary>
/// Normalized source evidence. Every later analysis links back to one of these items so
/// that counts, trends, and opportunity signals can be reproduced from persisted timestamps.
/// </summary>
public sealed class SourceItem
{
    /// <summary>
    /// Parameterless constructor for EF Core materialization. Application code MUST use
    /// the validating constructor so the source/policy invariants are enforced.
    /// </summary>
    private SourceItem()
    {
        ProviderId = string.Empty;
        ExternalId = string.Empty;
        CanonicalUrl = string.Empty;
        ProvenanceJson = "{}";
    }

    public SourceItem(
        Guid id,
        Guid projectId,
        string providerId,
        string externalId,
        string canonicalUrl,
        string? title,
        string? excerpt,
        DateTime? publishedUtc,
        DateTime observedUtc,
        string provenanceJson,
        ProviderObservation latestObservation,
        Guid? duplicateOfId = null)
    {
        if (id == Guid.Empty) throw new DomainValidationException("SourceItem.Id is required.");
        if (projectId == Guid.Empty) throw new DomainValidationException("SourceItem.ProjectId is required.");
        if (string.IsNullOrWhiteSpace(providerId)) throw new DomainValidationException("SourceItem.ProviderId is required.");
        if (string.IsNullOrWhiteSpace(externalId)) throw new DomainValidationException("SourceItem.ExternalId is required.");
        if (string.IsNullOrWhiteSpace(canonicalUrl)) throw new DomainValidationException("SourceItem.CanonicalUrl is required.");

        Id = id;
        ProjectId = projectId;
        ProviderId = providerId.Trim();
        ExternalId = externalId.Trim();
        CanonicalUrl = canonicalUrl.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Excerpt = string.IsNullOrWhiteSpace(excerpt) ? null : excerpt.Trim();
        PublishedUtc = publishedUtc?.ToUniversalTime();
        ObservedUtc = EnsureUtc(observedUtc);
        ProvenanceJson = provenanceJson ?? "{}";
        ApplyObservation(latestObservation ?? throw new ArgumentNullException(nameof(latestObservation)));
        DuplicateOfId = duplicateOfId;
    }

    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProviderId { get; set; }
    public string ExternalId { get; set; }
    public string CanonicalUrl { get; set; }
    public string? Title { get; set; }
    public string? Excerpt { get; set; }
    public DateTime? PublishedUtc { get; set; }
    public DateTime ObservedUtc { get; set; }
    public string ProvenanceJson { get; set; }
    public Guid? DuplicateOfId { get; set; }
    public DateTime CreatedUtc { get; set; }

    // LatestObservation fields are first-class columns on the parent table so EF
    // Core 10's InMemory provider can map them without shadow-property hydration
    // hooks. The LatestObservation value object is computed on demand.
    public string LatestObservationProviderId { get; set; } = "unknown";
    public ProviderStatus LatestObservationStatus { get; set; } = ProviderStatus.Available;
    public string? LatestObservationErrorCode { get; set; }
    public string? LatestObservationMessage { get; set; }
    public DateTime LatestObservationObservedUtc { get; set; } = DateTime.UtcNow;
    public int? LatestObservationHttpStatus { get; set; }

    public ProviderObservation LatestObservation => new(
        LatestObservationProviderId,
        LatestObservationStatus,
        LatestObservationErrorCode,
        LatestObservationMessage,
        LatestObservationObservedUtc,
        LatestObservationHttpStatus);

    public DateTime EffectiveDateUtc => PublishedUtc ?? ObservedUtc;

    public void RecordObservation(ProviderObservation observation, string? newProvenanceJson)
    {
        ApplyObservation(observation ?? throw new ArgumentNullException(nameof(observation)));
        if (!string.IsNullOrWhiteSpace(newProvenanceJson))
        {
            ProvenanceJson = newProvenanceJson;
        }
    }

    public void MarkDuplicateOf(Guid canonicalId, DateTime markedUtc)
    {
        if (canonicalId == Guid.Empty) throw new DomainValidationException("Duplicate target is required.");
        if (canonicalId == Id) throw new DomainValidationException("A source item cannot duplicate itself.");
        DuplicateOfId = canonicalId;
        var previous = LatestObservation;
        ApplyObservation(new ProviderObservation(
            previous.ProviderId,
            ProviderStatus.Available,
            "deduplicated",
            $"Marked duplicate of {canonicalId} at {EnsureUtc(markedUtc):O}",
            markedUtc));
    }

    private void ApplyObservation(ProviderObservation observation)
    {
        LatestObservationProviderId = observation.ProviderId;
        LatestObservationStatus = observation.Status;
        LatestObservationErrorCode = observation.ErrorCode;
        LatestObservationMessage = observation.Message;
        LatestObservationObservedUtc = observation.ObservedUtc;
        LatestObservationHttpStatus = observation.HttpStatus;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

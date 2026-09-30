namespace Kairion.Application.Abstractions;

/// <summary>
/// A normalized reference the application hands to a source provider when fetching a
/// previously-seen candidate. Providers map this to their native identifier.
/// </summary>
public sealed class SourceReference
{
    public SourceReference(Guid projectId, string providerId, string externalId, string canonicalUrl)
    {
        ProjectId = projectId;
        ProviderId = providerId;
        ExternalId = externalId;
        CanonicalUrl = canonicalUrl;
    }

    public Guid ProjectId { get; }
    public string ProviderId { get; }
    public string ExternalId { get; }
    public string CanonicalUrl { get; }
}

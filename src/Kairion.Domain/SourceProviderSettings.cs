namespace Kairion.Domain;

/// <summary>
/// Per-provider configuration for a research project. Secrets are never stored
/// here — only the credential reference name. Omitted providers default to
/// disabled. Limits are bounded: 1..5 queries per run, 1..50 results per query.
/// </summary>
public sealed record SourceProviderSettings
{
    public const int MinQueriesPerRun = 1;
    public const int MaxQueriesPerRunLimit = 5;
    public const int MinResultsPerQuery = 1;
    public const int MaxResultsPerQueryLimit = 50;

    public string ProviderId { get; }
    public bool Enabled { get; }
    public int MaxQueries { get; }
    public int MaxResultsPerQuery { get; }
    public string? Endpoint { get; }
    public string? CredentialRef { get; }

    public SourceProviderSettings(
        string providerId,
        bool enabled,
        int maxQueries = MaxQueriesPerRunLimit,
        int maxResultsPerQuery = MaxResultsPerQueryLimit,
        string? endpoint = null,
        string? credentialRef = null)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new DomainValidationException("providerId is required.");
        }
        if (maxQueries < MinQueriesPerRun || maxQueries > MaxQueriesPerRunLimit)
        {
            throw new DomainValidationException($"maxQueries must be between {MinQueriesPerRun} and {MaxQueriesPerRunLimit}.");
        }
        if (maxResultsPerQuery < MinResultsPerQuery || maxResultsPerQuery > MaxResultsPerQueryLimit)
        {
            throw new DomainValidationException($"maxResultsPerQuery must be between {MinResultsPerQuery} and {MaxResultsPerQueryLimit}.");
        }
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var trimmed = endpoint.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainValidationException("provider endpoint must be an absolute HTTPS URL.");
            }
            Endpoint = trimmed;
        }
        else
        {
            Endpoint = null;
        }

        ProviderId = providerId.Trim();
        Enabled = enabled;
        MaxQueries = maxQueries;
        MaxResultsPerQuery = maxResultsPerQuery;
        CredentialRef = string.IsNullOrWhiteSpace(credentialRef) ? null : credentialRef.Trim();
    }
}

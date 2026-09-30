using Kairion.Domain;

namespace Kairion.Application.Abstractions;

/// <summary>
/// Normalizes and validates a provider candidate before persistence. Enforces
/// HTTPS canonical URLs, title/excerpt size caps, and provenance hygiene:
/// raw response bodies and credentials are never persisted.
/// </summary>
public static class CandidateNormalizer
{
    public const int MaxTitleLength = 500;
    public const int MaxExcerptLength = 8_000;
    public const int MaxProvenanceBytes = 8_000;

    public sealed record NormalizedCandidate(
        string CanonicalUrl,
        string ExternalId,
        string? Title,
        string? Excerpt,
        string ProvenanceJson,
        string? AuthorHandle);

    public static bool TryNormalize(
        SourceFetchResult candidate,
        out NormalizedCandidate? normalized,
        out string failureCode)
    {
        normalized = null;
        failureCode = "ok";
        if (candidate is null)
        {
            failureCode = "null_candidate";
            return false;
        }
        if (string.IsNullOrWhiteSpace(candidate.ExternalId))
        {
            failureCode = "missing_external_id";
            return false;
        }
        if (!CanonicalUrl.TryNormalize(candidate.CanonicalUrl, out var canonical))
        {
            failureCode = "invalid_url";
            return false;
        }
        if (!Uri.TryCreate(canonical, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            failureCode = "non_https_url";
            return false;
        }

        var title = Truncate(candidate.Title?.Trim(), MaxTitleLength);
        var excerpt = Truncate(candidate.Excerpt?.Trim(), MaxExcerptLength);
        var provenance = SanitizeProvenance(candidate.ProvenanceJson);
        if (provenance is null)
        {
            failureCode = "oversized_provenance";
            return false;
        }

        normalized = new NormalizedCandidate(
            canonical,
            candidate.ExternalId.Trim(),
            string.IsNullOrWhiteSpace(title) ? null : title,
            string.IsNullOrWhiteSpace(excerpt) ? null : excerpt,
            provenance,
            string.IsNullOrWhiteSpace(candidate.AuthorHandle) ? null : candidate.AuthorHandle.Trim());
        return true;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    internal static string? SanitizeProvenance(string? provenanceJson)
    {
        var raw = string.IsNullOrWhiteSpace(provenanceJson) ? "{}" : provenanceJson.Trim();
        if (System.Text.Encoding.UTF8.GetByteCount(raw) > MaxProvenanceBytes)
        {
            return null;
        }
        foreach (var marker in new[] { "api_key", "apikey", "password", "secret", "Bearer ", "Basic ", "token=" })
        {
            if (raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }
        }
        return raw;
    }
}

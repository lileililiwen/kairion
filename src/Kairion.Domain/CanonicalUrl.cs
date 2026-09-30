namespace Kairion.Domain;

/// <summary>
/// Deterministic canonical-URL key used for idempotent ingestion. Normalization
/// lowercases scheme/host, drops fragments, trims trailing slashes, and trims
/// whitespace so the same discussion URL from two providers maps to one row.
/// Query strings are preserved because they can identify distinct items.
/// </summary>
public static class CanonicalUrl
{
    public static string Normalize(string canonicalUrl)
    {
        if (string.IsNullOrWhiteSpace(canonicalUrl))
        {
            throw new DomainValidationException("canonicalUrl is required.");
        }
        var trimmed = canonicalUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new DomainValidationException("canonicalUrl must be an absolute URL.");
        }
        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException("canonicalUrl must use http or https.");
        }
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
        };
        builder.Scheme = uri.Scheme.ToLowerInvariant();
        builder.Host = uri.Host.ToLowerInvariant();
        if ((builder.Scheme == "https" && builder.Port == 443)
            || (builder.Scheme == "http" && builder.Port == 80))
        {
            builder.Port = -1;
        }
        var normalized = builder.Uri.ToString().TrimEnd('/');
        return normalized;
    }

    public static bool TryNormalize(string? canonicalUrl, out string normalized)
    {
        normalized = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(canonicalUrl)) return false;
            normalized = Normalize(canonicalUrl);
            return true;
        }
        catch (DomainValidationException)
        {
            return false;
        }
    }
}

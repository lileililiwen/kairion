namespace Kairion.Domain;

/// <summary>
/// Provider-classified status of a single attempt. Used uniformly by source providers and
/// AI providers to make the resulting system behavior inspectable.
/// </summary>
public enum ProviderStatus
{
    /// <summary>The attempt succeeded and produced usable output.</summary>
    Available = 0,

    /// <summary>Provider rate-limited the call; back off and retry later.</summary>
    RateLimited = 1,

    /// <summary>Provider denied access because credentials were missing or wrong.</summary>
    Unauthorized = 2,

    /// <summary>Provider is unavailable; consider retrying with backoff.</summary>
    Unavailable = 3,

    /// <summary>The call timed out before the provider returned a usable result.</summary>
    TimedOut = 4,

    /// <summary>The provider returned a response that could not be parsed or validated.</summary>
    InvalidResponse = 5,

    /// <summary>The provider or source explicitly denied the request under its policy.</summary>
    PolicyDenied = 6,
}

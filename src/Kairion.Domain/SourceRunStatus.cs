namespace Kairion.Domain;

/// <summary>
/// Terminal status of a single per-provider ingestion run. Persisted on
/// <see cref="SourceIngestionRun"/> so provider health stays inspectable
/// without re-running the provider.
/// </summary>
public enum SourceRunStatus
{
    /// <summary>Provider skipped because it is disabled for the project.</summary>
    Disabled = 0,

    /// <summary>All requested pages completed and validated candidates were stored.</summary>
    Complete = 1,

    /// <summary>Some pages succeeded; validated candidates kept, failure recorded.</summary>
    Partial = 2,

    /// <summary>Provider reachable but returned no usable candidates (empty success).</summary>
    Unavailable = 3,

    /// <summary>No candidates stored; safe failure code recorded for retry.</summary>
    Failed = 4,
}

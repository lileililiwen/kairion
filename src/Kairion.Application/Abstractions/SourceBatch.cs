using Kairion.Domain;

namespace Kairion.Application.Abstractions;

/// <summary>
/// Bounded result of a single provider execution. The adapter returns zero or
/// more normalized candidates plus a terminal/partial status, a safe diagnostic
/// code (never credentials or raw payloads), an optional retry-after hint, and
/// the retrieval timestamp. One provider failure never discards another
/// provider's accepted candidates.
/// </summary>
public sealed class SourceBatch
{
    public SourceBatch(
        string providerId,
        IReadOnlyList<SourceFetchResult> candidates,
        SourceRunStatus status,
        string diagnosticCode,
        DateTime retrievedAtUtc,
        DateTime? retryAfterUtc = null)
    {
        ProviderId = (providerId ?? throw new ArgumentNullException(nameof(providerId))).Trim();
        Candidates = candidates ?? Array.Empty<SourceFetchResult>();
        Status = status;
        DiagnosticCode = string.IsNullOrWhiteSpace(diagnosticCode) ? "ok" : diagnosticCode.Trim();
        RetrievedAtUtc = retrievedAtUtc.Kind == DateTimeKind.Utc
            ? retrievedAtUtc
            : DateTime.SpecifyKind(retrievedAtUtc, DateTimeKind.Utc);
        RetryAfterUtc = retryAfterUtc?.ToUniversalTime();
    }

    public string ProviderId { get; }
    public IReadOnlyList<SourceFetchResult> Candidates { get; }
    public SourceRunStatus Status { get; }
    public string DiagnosticCode { get; }
    public DateTime RetrievedAtUtc { get; }
    public DateTime? RetryAfterUtc { get; }

    public static SourceBatch Empty(string providerId, SourceRunStatus status, string diagnosticCode, DateTime retrievedAtUtc, DateTime? retryAfterUtc = null) =>
        new(providerId, Array.Empty<SourceFetchResult>(), status, diagnosticCode, retrievedAtUtc, retryAfterUtc);
}

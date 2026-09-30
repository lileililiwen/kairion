namespace Kairion.Domain;

/// <summary>
/// Per-provider ingestion run evidence. One row per (project, provider, run) so
/// partial results, rate limits, timeouts, and malformed payloads stay visible
/// while accepted candidates from other providers remain usable.
/// </summary>
public sealed class SourceIngestionRun
{
    private SourceIngestionRun()
    {
        ProviderId = string.Empty;
        DiagnosticCode = string.Empty;
    }

    public SourceIngestionRun(
        Guid id,
        Guid projectId,
        Guid runId,
        string providerId,
        SourceRunStatus status,
        int candidateCount,
        string diagnosticCode,
        DateTime retrievedAtUtc,
        DateTime? retryAfterUtc = null)
    {
        if (id == Guid.Empty) throw new DomainValidationException("SourceIngestionRun.Id is required.");
        if (projectId == Guid.Empty) throw new DomainValidationException("SourceIngestionRun.ProjectId is required.");
        if (runId == Guid.Empty) throw new DomainValidationException("SourceIngestionRun.RunId is required.");
        if (string.IsNullOrWhiteSpace(providerId)) throw new DomainValidationException("SourceIngestionRun.ProviderId is required.");
        if (candidateCount < 0) throw new DomainValidationException("candidateCount cannot be negative.");

        Id = id;
        ProjectId = projectId;
        RunId = runId;
        ProviderId = providerId.Trim();
        Status = status;
        CandidateCount = candidateCount;
        DiagnosticCode = string.IsNullOrWhiteSpace(diagnosticCode) ? "ok" : diagnosticCode.Trim();
        RetrievedAtUtc = EnsureUtc(retrievedAtUtc);
        RetryAfterUtc = retryAfterUtc?.ToUniversalTime();
        CreatedUtc = RetrievedAtUtc;
    }

    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid RunId { get; set; }
    public string ProviderId { get; set; }
    public SourceRunStatus Status { get; set; }
    public int CandidateCount { get; set; }
    public string DiagnosticCode { get; set; }
    public DateTime RetrievedAtUtc { get; set; }
    public DateTime? RetryAfterUtc { get; set; }
    public DateTime CreatedUtc { get; set; }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

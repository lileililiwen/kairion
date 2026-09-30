namespace Kairion.Domain;

/// <summary>
/// A read-model entry that exposes deterministic cluster statistics alongside an
/// AI-assisted explanation. It is generated from persisted cluster evidence and is
/// explicitly informational; it is not a business validation.
/// </summary>
public sealed class OpportunitySignal
{
    public OpportunitySignal(
        Guid id,
        Guid clusterId,
        int evidenceVolume,
        decimal? percentGrowth,
        bool newSignal,
        IReadOnlyList<string> currentAlternatives,
        IReadOnlyList<string> workarounds,
        decimal confidence,
        string? aiExplanation,
        string trendLabel,
        string window,
        DateTime asOfUtc,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        DateTime generatedUtc)
    {
        if (id == Guid.Empty) throw new DomainValidationException("OpportunitySignal.Id is required.");
        if (clusterId == Guid.Empty) throw new DomainValidationException("OpportunitySignal.ClusterId is required.");
        if (evidenceVolume < 0) throw new DomainValidationException("evidenceVolume must be >= 0.");
        if (confidence < 0m || confidence > 1m) throw new DomainValidationException("confidence must be in [0,1].");
        if (string.IsNullOrWhiteSpace(window)) throw new DomainValidationException("window is required.");
        if (string.IsNullOrWhiteSpace(trendLabel)) throw new DomainValidationException("trendLabel is required.");

        Id = id;
        ClusterId = clusterId;
        EvidenceVolume = evidenceVolume;
        PercentGrowth = percentGrowth;
        NewSignal = newSignal;
        CurrentAlternatives = currentAlternatives ?? Array.Empty<string>();
        Workarounds = workarounds ?? Array.Empty<string>();
        Confidence = Math.Round(confidence, 4, MidpointRounding.ToEven);
        AiExplanation = string.IsNullOrWhiteSpace(aiExplanation) ? null : aiExplanation.Trim();
        TrendLabel = trendLabel.Trim();
        Window = window.Trim();
        AsOfUtc = EnsureUtc(asOfUtc);
        WindowStartUtc = EnsureUtc(windowStartUtc);
        WindowEndUtc = EnsureUtc(windowEndUtc);
        GeneratedUtc = EnsureUtc(generatedUtc);
    }

    public Guid Id { get; }
    public Guid ClusterId { get; }
    public int EvidenceVolume { get; }
    public decimal? PercentGrowth { get; }
    public bool NewSignal { get; }
    public IReadOnlyList<string> CurrentAlternatives { get; }
    public IReadOnlyList<string> Workarounds { get; }
    public decimal Confidence { get; }
    public string? AiExplanation { get; }
    public string TrendLabel { get; }
    public string Window { get; }
    public DateTime AsOfUtc { get; }
    public DateTime WindowStartUtc { get; }
    public DateTime WindowEndUtc { get; }
    public DateTime GeneratedUtc { get; }

    public static OpportunitySignal Empty(
        Guid id,
        Guid clusterId,
        DateTime asOfUtc,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        DateTime generatedUtc,
        string window,
        string reason)
    {
        return new OpportunitySignal(
            id,
            clusterId,
            evidenceVolume: 0,
            percentGrowth: null,
            newSignal: false,
            currentAlternatives: Array.Empty<string>(),
            workarounds: Array.Empty<string>(),
            confidence: 0m,
            aiExplanation: string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            trendLabel: "no_evidence",
            window,
            asOfUtc,
            windowStartUtc,
            windowEndUtc,
            generatedUtc);
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

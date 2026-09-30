namespace Kairion.Domain;

/// <summary>
/// Cheap stage-1 classification of a source item. Decides whether the more expensive
/// deep-analysis stage is worth running on the candidate.
/// </summary>
public sealed class ScreeningResult
{
    public ScreeningResult(
        Guid id,
        Guid sourceItemId,
        string analysisVersion,
        decimal relevance,
        decimal pain,
        decimal commercialHint,
        bool spam,
        ScreeningDecision decision,
        string providerId,
        string model,
        DateTime createdUtc,
        string? failureReason = null)
    {
        if (id == Guid.Empty) throw new DomainValidationException("ScreeningResult.Id is required.");
        if (sourceItemId == Guid.Empty) throw new DomainValidationException("ScreeningResult.SourceItemId is required.");
        if (string.IsNullOrWhiteSpace(analysisVersion)) throw new DomainValidationException("analysisVersion is required.");
        if (string.IsNullOrWhiteSpace(providerId)) throw new DomainValidationException("providerId is required.");
        if (string.IsNullOrWhiteSpace(model)) throw new DomainValidationException("model is required.");

        Id = id;
        SourceItemId = sourceItemId;
        AnalysisVersion = analysisVersion.Trim();
        Relevance = ValidateUnit(relevance, nameof(relevance));
        Pain = ValidateUnit(pain, nameof(pain));
        CommercialHint = ValidateUnit(commercialHint, nameof(commercialHint));
        Spam = spam;
        Decision = decision;
        ProviderId = providerId.Trim();
        Model = model.Trim();
        CreatedUtc = EnsureUtc(createdUtc);
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
    }

    public Guid Id { get; }
    public Guid SourceItemId { get; }
    public string AnalysisVersion { get; }
    public decimal Relevance { get; }
    public decimal Pain { get; }
    public decimal CommercialHint { get; }
    public bool Spam { get; }
    public ScreeningDecision Decision { get; }
    public string ProviderId { get; }
    public string Model { get; }
    public DateTime CreatedUtc { get; }
    public string? FailureReason { get; private set; }

    public static ScreeningResult Failure(
        Guid id,
        Guid sourceItemId,
        string analysisVersion,
        string providerId,
        string model,
        string failureReason,
        DateTime createdUtc)
    {
        return new ScreeningResult(
            id,
            sourceItemId,
            analysisVersion,
            relevance: 0m,
            pain: 0m,
            commercialHint: 0m,
            spam: false,
            decision: ScreeningDecision.Failed,
            providerId,
            model,
            createdUtc,
            failureReason);
    }

    private static decimal ValidateUnit(decimal value, string paramName)
    {
        if (value < 0m || value > 1m)
        {
            throw new DomainValidationException($"{paramName} must be in [0,1].");
        }
        return Math.Round(value, 4, MidpointRounding.ToEven);
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

namespace Kairion.Domain;

/// <summary>
/// Typed stage-2 analysis. The schema version, provider, and model are persisted alongside
/// the content so a later schema change can be reasoned about explicitly.
/// </summary>
public sealed class DeepAnalysis
{
    public DeepAnalysis(
        Guid id,
        Guid sourceItemId,
        string schemaVersion,
        string problem,
        string context,
        string currentSolution,
        string dissatisfaction,
        string workaround,
        string desiredOutcome,
        string category,
        string? priceSensitivity,
        decimal painStrength,
        decimal confidence,
        string providerId,
        string model,
        AnalysisStatus status,
        DateTime createdUtc,
        string? failureReason = null)
    {
        if (id == Guid.Empty) throw new DomainValidationException("DeepAnalysis.Id is required.");
        if (sourceItemId == Guid.Empty) throw new DomainValidationException("DeepAnalysis.SourceItemId is required.");
        if (string.IsNullOrWhiteSpace(schemaVersion)) throw new DomainValidationException("schemaVersion is required.");
        if (status == AnalysisStatus.Completed)
        {
            ValidateRequiredText(problem, nameof(problem), maxLength: 2_000);
            ValidateRequiredText(context, nameof(context), maxLength: 4_000);
            ValidateRequiredText(currentSolution, nameof(currentSolution), maxLength: 2_000);
            ValidateRequiredText(dissatisfaction, nameof(dissatisfaction), maxLength: 2_000);
            ValidateRequiredText(workaround, nameof(workaround), maxLength: 2_000);
            ValidateRequiredText(desiredOutcome, nameof(desiredOutcome), maxLength: 2_000);
            ValidateRequiredText(category, nameof(category), maxLength: 200);
        }

        Id = id;
        SourceItemId = sourceItemId;
        SchemaVersion = schemaVersion.Trim();
        Problem = (problem ?? string.Empty).Trim();
        Context = (context ?? string.Empty).Trim();
        CurrentSolution = (currentSolution ?? string.Empty).Trim();
        Dissatisfaction = (dissatisfaction ?? string.Empty).Trim();
        Workaround = (workaround ?? string.Empty).Trim();
        DesiredOutcome = (desiredOutcome ?? string.Empty).Trim();
        Category = (category ?? string.Empty).Trim();
        PriceSensitivity = string.IsNullOrWhiteSpace(priceSensitivity) ? null : priceSensitivity.Trim();
        PainStrength = ValidateUnit(painStrength, nameof(painStrength));
        Confidence = ValidateUnit(confidence, nameof(confidence));
        ProviderId = providerId.Trim();
        Model = model.Trim();
        Status = status;
        CreatedUtc = EnsureUtc(createdUtc);
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
    }

    public Guid Id { get; }
    public Guid SourceItemId { get; }
    public string SchemaVersion { get; }
    public string Problem { get; }
    public string Context { get; }
    public string CurrentSolution { get; }
    public string Dissatisfaction { get; }
    public string Workaround { get; }
    public string DesiredOutcome { get; }
    public string Category { get; }
    public string? PriceSensitivity { get; }
    public decimal PainStrength { get; }
    public decimal Confidence { get; }
    public string ProviderId { get; }
    public string Model { get; }
    public AnalysisStatus Status { get; }
    public DateTime CreatedUtc { get; }
    public string? FailureReason { get; }

    public static DeepAnalysis Failure(
        Guid id,
        Guid sourceItemId,
        string schemaVersion,
        string providerId,
        string model,
        string failureReason,
        DateTime createdUtc)
    {
        return new DeepAnalysis(
            id,
            sourceItemId,
            schemaVersion,
            problem: string.Empty,
            context: string.Empty,
            currentSolution: string.Empty,
            dissatisfaction: string.Empty,
            workaround: string.Empty,
            desiredOutcome: string.Empty,
            category: string.Empty,
            priceSensitivity: null,
            painStrength: 0m,
            confidence: 0m,
            providerId,
            model,
            AnalysisStatus.Failed,
            createdUtc,
            failureReason);
    }

    private static void ValidateRequiredText(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{paramName} is required for a completed deep analysis.");
        }
        if (value.Length > maxLength)
        {
            throw new DomainValidationException($"{paramName} must be {maxLength} characters or fewer.");
        }
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

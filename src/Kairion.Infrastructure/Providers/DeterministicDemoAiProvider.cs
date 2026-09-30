using System.Text.Json;
using Kairion.Application.Abstractions;
using Kairion.Application.Schemas;
using Kairion.Domain;
using Microsoft.Extensions.Logging;

namespace Kairion.Infrastructure.Providers;

/// <summary>
/// Deterministic AI provider used for local development, end-to-end demos, and the
/// integration tests. It returns schema-valid output derived from a hash of the input,
/// so the same source item always produces the same screening/analysis. It does not
/// perform any network calls; the JSON it emits is shaped to satisfy the application
/// schemas.
/// </summary>
public sealed class DeterministicDemoAiProvider : IAiProvider
{
    private readonly ILogger<DeterministicDemoAiProvider> _logger;
    public const string ProviderKey = "deterministic-demo";
    public string ProviderId => ProviderKey;
    public string DisplayName => "Deterministic demo AI (offline)";
    public bool RequiresCredentials => false;

    public DeterministicDemoAiProvider(ILogger<DeterministicDemoAiProvider> logger) => _logger = logger;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<AiResult<TOutput>> AnalyzeAsync<TOutput>(
        object input,
        AiSchemaDefinition schema,
        CancellationToken cancellationToken)
        where TOutput : class
    {
        if (schema.Name == AiSchemas.ScreeningV1.Name)
        {
            var json = BuildScreening(input);
            return Task.FromResult(Envelope<TOutput>(json, schema, typeof(ScreeningAiResponse)));
        }
        if (schema.Name == AiSchemas.DeepAnalysisV1.Name)
        {
            var json = BuildDeepAnalysis(input);
            return Task.FromResult(Envelope<TOutput>(json, schema, typeof(DeepAnalysisAiResponse)));
        }
        if (schema.Name == AiSchemas.ClusterExplanationV1.Name)
        {
            var json = BuildClusterExplanation(input);
            return Task.FromResult(Envelope<TOutput>(json, schema, typeof(ClusterExplanationAiResponse)));
        }
        return Task.FromResult(Failure<TOutput>(schema, "unsupported_schema"));
    }

    public Task<AiResult<float[]>?> EmbedAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult<AiResult<float[]>?>(null);

    private static string BuildScreening(object input)
    {
        var text = input?.ToString() ?? string.Empty;
        var hash = Math.Abs(text.GetHashCode());
        var relevance = Round4(0.55m + (hash % 30) / 100m);
        var pain = Round4(0.45m + (hash % 25) / 100m);
        var commercial = Round4(0.40m + (hash % 20) / 100m);
        var spam = (hash % 17) == 0;
        var decision = (!spam && relevance >= 0.30m && (pain >= 0.40m || commercial >= 0.30m)) ? "retain" : "discard";
        var dto = new ScreeningAiResponse
        {
            Relevance = relevance,
            Pain = pain,
            CommercialHint = commercial,
            Spam = spam,
            DecisionHint = decision,
            Reason = "Deterministic demo provider output.",
        };
        return JsonSerializer.Serialize(dto, SerializerOptions);
    }

    private static string BuildDeepAnalysis(object input)
    {
        var text = input?.ToString() ?? string.Empty;
        var hash = Math.Abs(text.GetHashCode());
        var pain = Round4(0.55m + (hash % 25) / 100m);
        var confidence = Round4(0.65m + (hash % 25) / 100m);
        var categories = new[] { "tooling", "billing", "onboarding", "performance", "support" };
        var category = categories[hash % categories.Length];
        var dto = new DeepAnalysisAiResponse
        {
            Problem = $"Discussion surfaces a recurring problem in '{category}'.",
            Context = Truncate(text, 320),
            CurrentSolution = "Users rely on manual workarounds and spreadsheets.",
            Dissatisfaction = "Manual workarounds do not scale and create reporting gaps.",
            Workaround = "Spreadsheet trackers, ad-hoc scripts, and informal notes.",
            DesiredOutcome = "A single tool that captures and tracks the issue automatically.",
            Category = category,
            PriceSensitivity = "medium",
            PainStrength = pain,
            Confidence = confidence,
            SuggestedClusterLabel = $"Recurring {category} friction",
            SuggestedClusterCategory = category,
        };
        return JsonSerializer.Serialize(dto, SerializerOptions);
    }

    private static string BuildClusterExplanation(object input)
    {
        var dto = new ClusterExplanationAiResponse
        {
            Explanation = "Evidence shows recurring mentions of manual workarounds and unmet automation needs.",
            Alternatives = new List<string>
            {
                "Manual spreadsheets and trackers.",
                "Ad-hoc scripts and personal notes.",
            },
            Workarounds = new List<string>
            {
                "Spreadsheet reconciliation.",
                "Email digests assembled by hand.",
            },
            Confidence = 0.7m,
        };
        return JsonSerializer.Serialize(dto, SerializerOptions);
    }

    private static AiResult<TOutput> Envelope<TOutput>(string json, AiSchemaDefinition schema, Type expected)
        where TOutput : class
    {
        var metadata = new AiCallMetadata(
            providerId: ProviderKey,
            model: "demo-v1",
            schemaVersion: schema.Version,
            status: ProviderStatus.Available,
            failureCode: null,
            failureMessage: null,
            promptTokens: null,
            completionTokens: null,
            calledAtUtc: DateTime.UtcNow);
        // Validate by round-tripping through the expected DTO.
        try
        {
            var document = JsonDocument.Parse(json);
            var typed = document.Deserialize(expected);
            if (typed is TOutput ok)
            {
                return new AiResult<TOutput>(ok, metadata);
            }
        }
        catch (JsonException)
        {
        }
        var failure = new AiCallMetadata(
            providerId: ProviderKey,
            model: "demo-v1",
            schemaVersion: schema.Version,
            status: ProviderStatus.InvalidResponse,
            failureCode: "demo_provider_self_check_failed",
            failureMessage: null,
            promptTokens: null,
            completionTokens: null,
            calledAtUtc: DateTime.UtcNow);
        return new AiResult<TOutput>(output: null, metadata: failure);
    }

    private static AiResult<TOutput> Failure<TOutput>(AiSchemaDefinition schema, string code)
        where TOutput : class
    {
        return new AiResult<TOutput>(
            output: null,
            metadata: new AiCallMetadata(
                providerId: ProviderKey,
                model: "demo-v1",
                schemaVersion: schema.Version,
                status: ProviderStatus.InvalidResponse,
                failureCode: code,
                failureMessage: null,
                promptTokens: null,
                completionTokens: null,
                calledAtUtc: DateTime.UtcNow));
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        // The schemas use snake_case property names, so the demo provider must emit
        // snake_case to match the schema validator and any real provider.
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
    };

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.ToEven);

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text.Substring(0, maxLength - 1) + "…";
}

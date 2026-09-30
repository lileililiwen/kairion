using Kairion.Application.Abstractions;

namespace Kairion.Application.Schemas;

/// <summary>
/// Versioned JSON schemas for the typed AI stages. The schema is passed to the provider
/// and stored alongside the analysis row so a later schema change can be reasoned about.
/// </summary>
public static class AiSchemas
{
    public static readonly AiSchemaDefinition ScreeningV1 = new(
        name: "kairion.screening",
        version: "1.0",
        schemaJson: """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Kairion screening v1",
          "type": "object",
          "required": ["relevance", "pain", "commercial_hint", "spam", "decision_hint"],
          "additionalProperties": false,
          "properties": {
            "relevance": { "type": "number", "minimum": 0, "maximum": 1 },
            "pain": { "type": "number", "minimum": 0, "maximum": 1 },
            "commercial_hint": { "type": "number", "minimum": 0, "maximum": 1 },
            "spam": { "type": "boolean" },
            "decision_hint": {
              "type": "string",
              "enum": ["retain", "discard"]
            },
            "reason": { "type": "string", "maxLength": 500 }
          }
        }
        """);

    public static readonly AiSchemaDefinition DeepAnalysisV1 = new(
        name: "kairion.deep-analysis",
        version: "1.0",
        schemaJson: """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Kairion deep analysis v1",
          "type": "object",
          "required": [
            "problem",
            "context",
            "current_solution",
            "dissatisfaction",
            "workaround",
            "desired_outcome",
            "category",
            "pain_strength",
            "confidence"
          ],
          "additionalProperties": false,
          "properties": {
            "problem": { "type": "string", "minLength": 1, "maxLength": 2000 },
            "context": { "type": "string", "minLength": 1, "maxLength": 4000 },
            "current_solution": { "type": "string", "minLength": 1, "maxLength": 2000 },
            "dissatisfaction": { "type": "string", "minLength": 1, "maxLength": 2000 },
            "workaround": { "type": "string", "minLength": 1, "maxLength": 2000 },
            "desired_outcome": { "type": "string", "minLength": 1, "maxLength": 2000 },
            "category": { "type": "string", "minLength": 1, "maxLength": 200 },
            "price_sensitivity": {
              "type": ["string", "null"],
              "enum": ["low", "medium", "high", "unknown", null]
            },
            "pain_strength": { "type": "number", "minimum": 0, "maximum": 1 },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
            "suggested_cluster_label": { "type": "string", "maxLength": 200 },
            "suggested_cluster_category": { "type": "string", "maxLength": 200 }
          }
        }
        """);

    public static readonly AiSchemaDefinition ClusterExplanationV1 = new(
        name: "kairion.cluster-explanation",
        version: "1.0",
        schemaJson: """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Kairion cluster explanation v1",
          "type": "object",
          "required": ["explanation", "alternatives", "workarounds", "confidence"],
          "additionalProperties": false,
          "properties": {
            "explanation": { "type": "string", "minLength": 1, "maxLength": 1000 },
            "alternatives": {
              "type": "array",
              "maxItems": 10,
              "items": { "type": "string", "maxLength": 200 }
            },
            "workarounds": {
              "type": "array",
              "maxItems": 10,
              "items": { "type": "string", "maxLength": 200 }
            },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
          }
        }
        """);
}

/// <summary>
/// Typed DTOs that match the AI schemas above. Persisted as JSON in the database; the
/// C# representation is also the runtime type validated by the AI provider.
/// </summary>
public sealed class ScreeningAiResponse
{
    public decimal Relevance { get; set; }
    public decimal Pain { get; set; }
    public decimal CommercialHint { get; set; }
    public bool Spam { get; set; }
    public string DecisionHint { get; set; } = "discard";
    public string? Reason { get; set; }
}

public sealed class DeepAnalysisAiResponse
{
    public string Problem { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string CurrentSolution { get; set; } = string.Empty;
    public string Dissatisfaction { get; set; } = string.Empty;
    public string Workaround { get; set; } = string.Empty;
    public string DesiredOutcome { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? PriceSensitivity { get; set; }
    public decimal PainStrength { get; set; }
    public decimal Confidence { get; set; }
    public string? SuggestedClusterLabel { get; set; }
    public string? SuggestedClusterCategory { get; set; }
}

public sealed class ClusterExplanationAiResponse
{
    public string Explanation { get; set; } = string.Empty;
    public List<string> Alternatives { get; set; } = new();
    public List<string> Workarounds { get; set; } = new();
    public decimal Confidence { get; set; }
}

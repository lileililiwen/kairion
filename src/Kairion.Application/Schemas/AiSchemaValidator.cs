using System.Text.Json;
using System.Text.Json.Serialization;
using Kairion.Application.Abstractions;
using Kairion.Domain;

namespace Kairion.Application.Schemas;

/// <summary>
/// Strictly validates a candidate JSON value against a JSON schema (parsed from
/// <see cref="AiSchemaDefinition.SchemaJson"/>) and returns the typed DTO. The validator
/// runs the JSON-Schema-check-style structural rules the application requires and
/// additionally enforces the typed range invariants of the destination DTOs so an AI
/// output with out-of-range numerics is rejected before persistence.
/// </summary>
public sealed class AiSchemaValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        // The schemas are versioned and use snake_case property names; align the
        // serializer so round-tripping through a real provider matches the schema.
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>
    /// Validates and parses the supplied JSON text against the schema. Returns the typed
    /// output plus a metadata record describing the validation outcome. On failure the
    /// returned metadata has <see cref="ProviderStatus.InvalidResponse"/> with a
    /// non-sensitive reason code.
    /// </summary>
    public AiResult<TOutput> ValidateAndDeserialize<TOutput>(
        string rawJson,
        AiSchemaDefinition schema)
        where TOutput : class
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return Failure<TOutput>(schema, "empty_response");
        }

        JsonDocument? document = null;
        try
        {
            try
            {
                document = JsonDocument.Parse(rawJson);
            }
            catch (JsonException ex)
            {
                return Failure<TOutput>(schema, $"json_parse_error:{ex.Message}");
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failure<TOutput>(schema, "root_not_object");
            }

            var required = ExtractRequiredFields(schema.SchemaJson);
            if (required is not null)
            {
                foreach (var name in required)
                {
                    if (!document.RootElement.TryGetProperty(name, out _))
                    {
                        return Failure<TOutput>(schema, $"missing_required:{name}");
                    }
                }
            }

            if (ExtractAllowedAdditional(schema.SchemaJson) == false)
            {
                var allowed = AllowedPropertyNames(schema.SchemaJson);
                foreach (var prop in document.RootElement.EnumerateObject())
                {
                    if (allowed is not null && !allowed.Contains(prop.Name))
                    {
                        return Failure<TOutput>(schema, $"unknown_property:{prop.Name}");
                    }
                }
            }

            TOutput? typed;
            try
            {
                typed = document.RootElement.Deserialize<TOutput>(SerializerOptions);
            }
            catch (JsonException ex)
            {
                return Failure<TOutput>(schema, $"deserialize_error:{ex.Message}");
            }
            if (typed is null)
            {
                return Failure<TOutput>(schema, "deserialize_returned_null");
            }

            // Range checks per type.
            var rangeError = CheckRange(typed);
            if (rangeError is not null)
            {
                return Failure<TOutput>(schema, $"range_error:{rangeError}");
            }

            return new AiResult<TOutput>(
                typed,
                new AiCallMetadata(
                    providerId: "schema-validator",
                    model: "n/a",
                    schemaVersion: schema.Version,
                    status: ProviderStatus.Available,
                    failureCode: null,
                    failureMessage: null,
                    promptTokens: null,
                    completionTokens: null,
                    calledAtUtc: DateTime.UtcNow));
        }
        finally
        {
            document?.Dispose();
        }
    }

    private static string? CheckRange<TOutput>(TOutput value) where TOutput : class
    {
        switch (value)
        {
            case ScreeningAiResponse s:
                if (s.Relevance is < 0m or > 1m) return "relevance";
                if (s.Pain is < 0m or > 1m) return "pain";
                if (s.CommercialHint is < 0m or > 1m) return "commercial_hint";
                if (s.DecisionHint is not ("retain" or "discard")) return "decision_hint";
                return null;
            case DeepAnalysisAiResponse d:
                if (d.PainStrength is < 0m or > 1m) return "pain_strength";
                if (d.Confidence is < 0m or > 1m) return "confidence";
                if (d.PriceSensitivity is not null && d.PriceSensitivity is not ("low" or "medium" or "high" or "unknown"))
                    return "price_sensitivity";
                if (string.IsNullOrWhiteSpace(d.Problem)) return "problem_empty";
                if (string.IsNullOrWhiteSpace(d.Context)) return "context_empty";
                if (string.IsNullOrWhiteSpace(d.CurrentSolution)) return "current_solution_empty";
                if (string.IsNullOrWhiteSpace(d.Dissatisfaction)) return "dissatisfaction_empty";
                if (string.IsNullOrWhiteSpace(d.Workaround)) return "workaround_empty";
                if (string.IsNullOrWhiteSpace(d.DesiredOutcome)) return "desired_outcome_empty";
                if (string.IsNullOrWhiteSpace(d.Category)) return "category_empty";
                return null;
            case ClusterExplanationAiResponse c:
                if (c.Confidence is < 0m or > 1m) return "confidence";
                if (string.IsNullOrWhiteSpace(c.Explanation)) return "explanation_empty";
                return null;
            default:
                return null;
        }
    }

    private static AiResult<TOutput> Failure<TOutput>(AiSchemaDefinition schema, string code) where TOutput : class
    {
        return new AiResult<TOutput>(
            output: null,
            metadata: new AiCallMetadata(
                providerId: "schema-validator",
                model: "n/a",
                schemaVersion: schema.Version,
                status: ProviderStatus.InvalidResponse,
                failureCode: code,
                failureMessage: null,
                promptTokens: null,
                completionTokens: null,
                calledAtUtc: DateTime.UtcNow));
    }

    private static IReadOnlyList<string>? ExtractRequiredFields(string schemaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("required", out var required) ||
                required.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
            var list = new List<string>();
            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var v = item.GetString();
                    if (!string.IsNullOrWhiteSpace(v)) list.Add(v);
                }
            }
            return list;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool? ExtractAllowedAdditional(string schemaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("additionalProperties", out var additional)) return null;
            return additional.ValueKind switch
            {
                JsonValueKind.False => false,
                JsonValueKind.True => true,
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static HashSet<string>? AllowedPropertyNames(string schemaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("properties", out var properties) ||
                properties.ValueKind != JsonValueKind.Object) return null;
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in properties.EnumerateObject())
            {
                set.Add(prop.Name);
            }
            return set;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

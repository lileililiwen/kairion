using System;
using FluentAssertions;
using Kairion.Application.Schemas;
using Xunit;

namespace Kairion.UnitTests.Schemas;

/// <summary>
/// Tests that the AI schema validator enforces the JSON-schema-style structural rules
/// the application promises to providers: required fields present, no unknown properties
/// when <c>additionalProperties:false</c>, and typed range checks for known DTOs.
/// </summary>
public class AiSchemaValidatorTests
{
    private readonly AiSchemaValidator _validator = new();

    [Fact]
    public void Screening_ValidPayload_ProducesAvailableResult()
    {
        var json = """
        {
          "relevance": 0.7,
          "pain": 0.6,
          "commercial_hint": 0.5,
          "spam": false,
          "decision_hint": "retain"
        }
        """;
        var result = _validator.ValidateAndDeserialize<ScreeningAiResponse>(json, AiSchemas.ScreeningV1);
        result.Output.Should().NotBeNull();
        result.Metadata.Status.Should().Be(Kairion.Domain.ProviderStatus.Available);
        result.Output!.Relevance.Should().Be(0.7m);
    }

    [Fact]
    public void Screening_MissingRequiredField_IsRejected()
    {
        var json = """
        {
          "relevance": 0.7,
          "pain": 0.6,
          "spam": false,
          "decision_hint": "retain"
        }
        """;
        var result = _validator.ValidateAndDeserialize<ScreeningAiResponse>(json, AiSchemas.ScreeningV1);
        result.Output.Should().BeNull();
        result.Metadata.Status.Should().Be(Kairion.Domain.ProviderStatus.InvalidResponse);
        result.Metadata.FailureCode.Should().StartWith("missing_required:commercial_hint");
    }

    [Fact]
    public void Screening_UnknownProperty_IsRejected()
    {
        var json = """
        {
          "relevance": 0.7,
          "pain": 0.6,
          "commercial_hint": 0.5,
          "spam": false,
          "decision_hint": "retain",
          "sneaky": 42
        }
        """;
        var result = _validator.ValidateAndDeserialize<ScreeningAiResponse>(json, AiSchemas.ScreeningV1);
        result.Output.Should().BeNull();
        result.Metadata.FailureCode.Should().StartWith("unknown_property:sneaky");
    }

    [Fact]
    public void Screening_OutOfRangeRelevance_IsRejected()
    {
        var json = """
        {
          "relevance": 1.5,
          "pain": 0.6,
          "commercial_hint": 0.5,
          "spam": false,
          "decision_hint": "retain"
        }
        """;
        var result = _validator.ValidateAndDeserialize<ScreeningAiResponse>(json, AiSchemas.ScreeningV1);
        result.Output.Should().BeNull();
        result.Metadata.FailureCode.Should().StartWith("range_error:relevance");
    }

    [Fact]
    public void Screening_InvalidDecisionHint_IsRejected()
    {
        var json = """
        {
          "relevance": 0.7,
          "pain": 0.6,
          "commercial_hint": 0.5,
          "spam": false,
          "decision_hint": "unsure"
        }
        """;
        var result = _validator.ValidateAndDeserialize<ScreeningAiResponse>(json, AiSchemas.ScreeningV1);
        result.Output.Should().BeNull();
        result.Metadata.FailureCode.Should().StartWith("range_error:decision_hint");
    }

    [Fact]
    public void DeepAnalysis_ValidPayload_ProducesAvailableResult()
    {
        var json = """
        {
          "problem": "P",
          "context": "C",
          "current_solution": "CS",
          "dissatisfaction": "D",
          "workaround": "W",
          "desired_outcome": "DO",
          "category": "cat",
          "pain_strength": 0.5,
          "confidence": 0.7
        }
        """;
        var result = _validator.ValidateAndDeserialize<DeepAnalysisAiResponse>(json, AiSchemas.DeepAnalysisV1);
        result.Output.Should().NotBeNull();
        result.Metadata.Status.Should().Be(Kairion.Domain.ProviderStatus.Available);
    }

    [Fact]
    public void DeepAnalysis_OutOfRangeConfidence_IsRejected()
    {
        var json = """
        {
          "problem": "P",
          "context": "C",
          "current_solution": "CS",
          "dissatisfaction": "D",
          "workaround": "W",
          "desired_outcome": "DO",
          "category": "cat",
          "pain_strength": 0.5,
          "confidence": 1.1
        }
        """;
        var result = _validator.ValidateAndDeserialize<DeepAnalysisAiResponse>(json, AiSchemas.DeepAnalysisV1);
        result.Output.Should().BeNull();
        result.Metadata.FailureCode.Should().StartWith("range_error:confidence");
    }

    [Fact]
    public void DeepAnalysis_MissingProblem_IsRejected()
    {
        var json = """
        {
          "problem": "",
          "context": "C",
          "current_solution": "CS",
          "dissatisfaction": "D",
          "workaround": "W",
          "desired_outcome": "DO",
          "category": "cat",
          "pain_strength": 0.5,
          "confidence": 0.7
        }
        """;
        var result = _validator.ValidateAndDeserialize<DeepAnalysisAiResponse>(json, AiSchemas.DeepAnalysisV1);
        result.Output.Should().BeNull();
        result.Metadata.FailureCode.Should().StartWith("range_error:problem_empty");
    }

    [Fact]
    public void EmptyJson_IsRejected()
    {
        var result = _validator.ValidateAndDeserialize<ScreeningAiResponse>("", AiSchemas.ScreeningV1);
        result.Output.Should().BeNull();
        result.Metadata.FailureCode.Should().Be("empty_response");
    }

    [Fact]
    public void NotJsonObject_IsRejected()
    {
        var result = _validator.ValidateAndDeserialize<ScreeningAiResponse>("[1,2,3]", AiSchemas.ScreeningV1);
        result.Output.Should().BeNull();
        result.Metadata.FailureCode.Should().Be("root_not_object");
    }

    [Fact]
    public void MalformedJson_IsRejected()
    {
        var result = _validator.ValidateAndDeserialize<ScreeningAiResponse>("{not json", AiSchemas.ScreeningV1);
        result.Output.Should().BeNull();
        result.Metadata.FailureCode.Should().StartWith("json_parse_error");
    }

    [Fact]
    public void ClusterExplanation_ValidPayload_IsAccepted()
    {
        var json = """
        {
          "explanation": "Recurring manual workarounds.",
          "alternatives": ["Spreadsheets"],
          "workarounds": ["Email digests"],
          "confidence": 0.6
        }
        """;
        var result = _validator.ValidateAndDeserialize<ClusterExplanationAiResponse>(json, AiSchemas.ClusterExplanationV1);
        result.Output.Should().NotBeNull();
        result.Output!.Alternatives.Should().ContainSingle();
    }

    [Fact]
    public void ClusterExplanation_OutOfRangeConfidence_IsRejected()
    {
        var json = """
        {
          "explanation": "Recurring manual workarounds.",
          "alternatives": ["Spreadsheets"],
          "workarounds": ["Email digests"],
          "confidence": 2.0
        }
        """;
        var result = _validator.ValidateAndDeserialize<ClusterExplanationAiResponse>(json, AiSchemas.ClusterExplanationV1);
        result.Output.Should().BeNull();
        result.Metadata.FailureCode.Should().StartWith("range_error:confidence");
    }
}

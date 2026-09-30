using FluentAssertions;
using Kairion.Application.Schemas;
using Kairion.Application.UseCases;
using Kairion.Domain;
using Xunit;

namespace Kairion.UnitTests.UseCases;

/// <summary>
/// Tests the threshold-based screening decision policy. The default policy must reject
/// spam, low-relevance, and low-pain items, and accept items that meet at least one of
/// the pain / commercial / explicit-hint thresholds.
/// </summary>
public class ScreeningDecisionPolicyTests
{
    private static readonly ScreeningDecisionPolicy Default = ScreeningDecisionPolicy.Default;

    [Fact]
    public void Spam_AlwaysDiscarded()
    {
        var dto = new ScreeningAiResponse
        {
            Relevance = 0.99m,
            Pain = 0.99m,
            CommercialHint = 0.99m,
            Spam = true,
            DecisionHint = "retain",
        };
        Default.Decide(dto).Should().Be(ScreeningDecision.Discard);
    }

    [Fact]
    public void LowRelevance_Discarded()
    {
        var dto = new ScreeningAiResponse
        {
            Relevance = 0.10m,
            Pain = 0.99m,
            CommercialHint = 0.99m,
            Spam = false,
            DecisionHint = "retain",
        };
        Default.Decide(dto).Should().Be(ScreeningDecision.Discard);
    }

    [Fact]
    public void HighPainAndRelevance_Retained()
    {
        var dto = new ScreeningAiResponse
        {
            Relevance = 0.50m,
            Pain = 0.60m,
            CommercialHint = 0.10m,
            Spam = false,
            DecisionHint = "discard",
        };
        Default.Decide(dto).Should().Be(ScreeningDecision.Retain);
    }

    [Fact]
    public void ModerateCommercialHint_Retained()
    {
        var dto = new ScreeningAiResponse
        {
            Relevance = 0.40m,
            Pain = 0.20m,
            CommercialHint = 0.40m,
            Spam = false,
            DecisionHint = "discard",
        };
        Default.Decide(dto).Should().Be(ScreeningDecision.Retain);
    }

    [Fact]
    public void ExplicitRetainHint_Accepted()
    {
        var dto = new ScreeningAiResponse
        {
            Relevance = 0.40m,
            Pain = 0.20m,
            CommercialHint = 0.10m,
            Spam = false,
            DecisionHint = "retain",
        };
        Default.Decide(dto).Should().Be(ScreeningDecision.Retain);
    }

    [Fact]
    public void NoneOfThresholdsMet_Discarded()
    {
        var dto = new ScreeningAiResponse
        {
            Relevance = 0.40m,
            Pain = 0.20m,
            CommercialHint = 0.10m,
            Spam = false,
            DecisionHint = "discard",
        };
        Default.Decide(dto).Should().Be(ScreeningDecision.Discard);
    }

    [Fact]
    public void Constructor_RejectsOutOfRangeThresholds()
    {
        var act = () => new ScreeningDecisionPolicy(relevanceFloor: -0.1m, painFloor: 0.4m, commercialHintFloor: 0.3m);
        act.Should().Throw<System.ArgumentOutOfRangeException>();
    }
}

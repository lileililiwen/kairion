using Kairion.Application.Schemas;
using Kairion.Domain;

namespace Kairion.Application.UseCases;

/// <summary>
/// Versioned threshold-based policy that turns an AI screening result into a
/// <see cref="ScreeningDecision"/>. The default policy is conservative: spam is always
/// discarded; clearly irrelevant items are discarded; everything else is decided by the
/// AI hint plus relevance and pain thresholds.
/// </summary>
public sealed class ScreeningDecisionPolicy
{
    public ScreeningDecisionPolicy(decimal relevanceFloor, decimal painFloor, decimal commercialHintFloor)
    {
        if (relevanceFloor < 0m || relevanceFloor > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(relevanceFloor));
        }
        if (painFloor < 0m || painFloor > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(painFloor));
        }
        if (commercialHintFloor < 0m || commercialHintFloor > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(commercialHintFloor));
        }
        RelevanceFloor = relevanceFloor;
        PainFloor = painFloor;
        CommercialHintFloor = commercialHintFloor;
    }

    public static readonly ScreeningDecisionPolicy Default = new(
        relevanceFloor: 0.30m,
        painFloor: 0.40m,
        commercialHintFloor: 0.30m);

    public decimal RelevanceFloor { get; }
    public decimal PainFloor { get; }
    public decimal CommercialHintFloor { get; }

    public ScreeningDecision Decide(ScreeningAiResponse response)
    {
        if (response.Spam) return ScreeningDecision.Discard;
        if (response.Relevance < RelevanceFloor) return ScreeningDecision.Discard;
        if (response.Pain >= PainFloor) return ScreeningDecision.Retain;
        if (response.CommercialHint >= CommercialHintFloor) return ScreeningDecision.Retain;
        if (string.Equals(response.DecisionHint, "retain", StringComparison.OrdinalIgnoreCase))
        {
            return ScreeningDecision.Retain;
        }
        return ScreeningDecision.Discard;
    }
}

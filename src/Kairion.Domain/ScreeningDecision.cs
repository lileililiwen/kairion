namespace Kairion.Domain;

/// <summary>
/// Outcome of the screening stage. <see cref="Retain"/> candidates continue to deep
/// analysis; <see cref="Discard"/> and <see cref="Failed"/> skip or expose the failure
/// without losing source evidence.
/// </summary>
public enum ScreeningDecision
{
    /// <summary>No decision yet recorded.</summary>
    Pending = 0,
    /// <summary>Candidate passed screening and should be deep-analyzed.</summary>
    Retain = 1,
    /// <summary>Candidate was not relevant, not painful, or spam; deep analysis skipped.</summary>
    Discard = 2,
    /// <summary>Provider call failed; candidate is left available for retry.</summary>
    Failed = 3,
}

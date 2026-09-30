namespace Kairion.Domain;

/// <summary>
/// Lifecycle state of a deep analysis. The source item is never deleted when analysis
/// fails; the failure status is recorded for retry.
/// </summary>
public enum AnalysisStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
}

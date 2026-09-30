namespace Kairion.Domain;

/// <summary>
/// The kind of human revision recorded against the cluster / assignment state.
/// </summary>
public enum HumanRevisionAction
{
    /// <summary>The owner edited a cluster's label, category, or summary.</summary>
    EditedCluster = 1,
    /// <summary>The owner moved a source item to a different cluster.</summary>
    ReassignedItem = 2,
    /// <summary>The owner merged two clusters into one.</summary>
    MergedClusters = 3,
    /// <summary>The owner split a cluster into two or more clusters.</summary>
    SplitCluster = 4,
    /// <summary>The owner marked a source as a false positive.</summary>
    MarkedFalsePositive = 5,
}

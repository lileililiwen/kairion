using Kairion.Domain;

namespace Kairion.Application.Abstractions;

/// <summary>
/// Specific read APIs the application needs to compute trends and clusters. The interface
/// is intentionally narrow so it can be implemented in EF Core or, in tests, in memory.
/// </summary>
public interface IObservationReadService
{
    /// <summary>Returns distinct source item ids whose effective date falls in the window.</summary>
    Task<IReadOnlyList<Guid>> DistinctSourceIdsInWindowAsync(
        Guid projectId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        Guid? clusterId,
        CancellationToken cancellationToken);

    /// <summary>Returns the earliest known first-observed timestamp for the source item.</summary>
    Task<DateTime?> FirstObservedUtcForAsync(Guid sourceItemId, CancellationToken cancellationToken);
}

/// <summary>
/// Provider lookup so use cases can pick a configured source or AI provider by id.
/// </summary>
public interface IProviderRegistry
{
    ISourceProvider? FindSourceProvider(string providerId);
    IAiProvider? FindAiProvider(string providerId);
    IReadOnlyList<ISourceProvider> ListSourceProviders();
    IReadOnlyList<IAiProvider> ListAiProviders();
}

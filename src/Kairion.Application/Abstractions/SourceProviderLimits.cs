namespace Kairion.Application.Abstractions;

/// <summary>
/// Bounds enforced on every provider execution: at most 5 queries per run,
/// at most 50 results per query, 1 MiB maximum response body, 10s HTTP
/// timeout, per-provider concurrency of 1, and at most 3 attempts with
/// bounded exponential backoff.
/// </summary>
public static class SourceProviderLimits
{
    public const int MaxQueriesPerRun = 5;
    public const int MaxResultsPerQuery = 50;
    public const int MaxResponseBytes = 1 * 1024 * 1024;
    public const int MaxAttempts = 3;
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MinProviderInterval = TimeSpan.FromSeconds(1);

    public static int BoundResultsPerQuery(int requested) =>
        Math.Clamp(requested <= 0 ? 25 : requested, 1, MaxResultsPerQuery);

    public static int BoundQueries(int requested) =>
        Math.Clamp(requested <= 0 ? MaxQueriesPerRun : requested, 1, MaxQueriesPerRun);
}

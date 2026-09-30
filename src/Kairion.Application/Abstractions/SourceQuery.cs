namespace Kairion.Application.Abstractions;

/// <summary>
/// A normalized query the application hands to a source provider. Provider implementations
/// translate this into their native query format. The query is the owner-authored
/// strategy; the provider MUST NOT introduce new terms or relax the strategy without an
/// owner action.
/// </summary>
public sealed class SourceQuery
{
    public SourceQuery(
        Guid projectId,
        string text,
        IReadOnlyList<string> topics,
        IReadOnlyList<string> competitors,
        DateTime? windowStartUtc,
        DateTime? windowEndUtc,
        int maxResults,
        int maxQueries = SourceProviderLimits.MaxQueriesPerRun)
    {
        ProjectId = projectId;
        Text = text ?? string.Empty;
        Topics = topics ?? Array.Empty<string>();
        Competitors = competitors ?? Array.Empty<string>();
        WindowStartUtc = windowStartUtc?.ToUniversalTime();
        WindowEndUtc = windowEndUtc?.ToUniversalTime();
        MaxResults = SourceProviderLimits.BoundResultsPerQuery(maxResults);
        MaxQueries = SourceProviderLimits.BoundQueries(maxQueries);
    }

    public Guid ProjectId { get; }
    public string Text { get; }
    public IReadOnlyList<string> Topics { get; }
    public IReadOnlyList<string> Competitors { get; }
    public DateTime? WindowStartUtc { get; }
    public DateTime? WindowEndUtc { get; }
    public int MaxResults { get; }
    public int MaxQueries { get; }
}

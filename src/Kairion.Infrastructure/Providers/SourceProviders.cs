using System.Security.Cryptography;
using System.Text;
using Kairion.Application.Abstractions;
using Kairion.Domain;
using Microsoft.Extensions.Logging;

namespace Kairion.Infrastructure.Providers;

/// <summary>
/// The manual-URL provider. Always available; does not require credentials. Used as the
/// default intake path for the MVP and as a regression target for the source-provider
/// contract tests.
/// </summary>
public sealed class ManualSourceProvider : ISourceProvider
{
    public const string ProviderKey = "manual";
    public string ProviderId => ProviderKey;
    public string DisplayName => "Manual URL intake";
    public bool RequiresCredentials => false;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<SourceBatch> SearchAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        // Manual provider does not perform autonomous searches. A search against it
        // returns an empty Complete batch so the UI can show that this provider
        // does not auto-discover candidates.
        return Task.FromResult(SourceBatch.Empty(
            ProviderKey, SourceRunStatus.Complete, "manual_no_search", DateTime.UtcNow));
    }

    public Task<SourceFetchResult?> FetchAsync(SourceReference reference, CancellationToken cancellationToken) =>
        Task.FromResult<SourceFetchResult?>(null);
}

/// <summary>
/// Stub search/source provider used for local development and end-to-end demos. It uses
/// a deterministic hash of the query text to synthesize a small set of mock candidates
/// the application can persist, screen, and cluster. It does not make any network calls.
/// </summary>
public sealed class DemoSearchSourceProvider : ISourceProvider
{
    private readonly ILogger<DemoSearchSourceProvider> _logger;
    public const string ProviderKey = "demo-search";
    public string ProviderId => ProviderKey;
    public string DisplayName => "Demo search (offline stub)";
    public bool RequiresCredentials => false;

    public DemoSearchSourceProvider(ILogger<DemoSearchSourceProvider> logger) => _logger = logger;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<SourceBatch> SearchAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var results = new List<SourceFetchResult>();
        for (var i = 0; i < Math.Min(query.MaxResults, 5); i++)
        {
            var externalId = DeterministicId(query.ProjectId, query.Text, i);
            var canonicalUrl = $"https://demo.invalid/{externalId}";
            var published = now.AddDays(-i * 2 - 1);
            var observation = new ProviderObservation(
                providerId: ProviderKey,
                status: ProviderStatus.Available,
                errorCode: null,
                message: null,
                observedUtc: now,
                httpStatus: 200);
            results.Add(new SourceFetchResult(
                providerId: ProviderKey,
                externalId: externalId,
                canonicalUrl: canonicalUrl,
                title: $"Demo candidate #{i + 1} for {Truncate(query.Text, 60)}",
                excerpt: $"Synthetic discussion snippet for {Truncate(query.Text, 80)}. Discusses workarounds, current solution, and pricing.",
                publishedUtc: published,
                observedUtc: now,
                provenanceJson: $"{{\"source\":\"{ProviderKey}\",\"queryHash\":\"{Hash(query.Text)}\",\"index\":{i}}}",
                observation: observation));
        }
        _logger.LogInformation("DemoSearchSourceProvider returned {Count} deterministic candidates for project {ProjectId}.", results.Count, query.ProjectId);
        return Task.FromResult(new SourceBatch(ProviderKey, results, SourceRunStatus.Complete, "ok", now));
    }

    public Task<SourceFetchResult?> FetchAsync(SourceReference reference, CancellationToken cancellationToken) =>
        Task.FromResult<SourceFetchResult?>(null);

    private static string DeterministicId(Guid projectId, string text, int index)
    {
        var input = $"{projectId:N}|{text}|{index}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).Substring(0, 24).ToLowerInvariant();
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty))).Substring(0, 12).ToLowerInvariant();

    private static string Truncate(string text, int maxLength) =>
        string.IsNullOrEmpty(text) ? string.Empty :
        text.Length <= maxLength ? text :
        text.Substring(0, maxLength - 1) + "…";
}

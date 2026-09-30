using System.Net;
using System.Text.Json;
using Kairion.Application.Abstractions;
using Kairion.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kairion.Infrastructure.Providers;

/// <summary>
/// Options for the Hacker News adapter. The public Algolia HN search API is
/// the default; operators may override the base URL (HTTPS only) for mirrors.
/// </summary>
public sealed class HackerNewsOptions
{
    public string BaseUrl { get; set; } = "https://hn.algolia.com/api/v1/";
}

/// <summary>
/// Hacker News source adapter behind the <see cref="ISourceProvider"/> contract.
/// Uses the public Algolia HN search API (documented, no scraping), bounded to
/// at most 5 queries/run and 50 results/query with a 1 MiB response cap, 10s
/// timeout, and at most 3 attempts with bounded backoff. Returns normalized
/// candidates plus a Complete/Partial/Failed batch status with safe diagnostics.
/// </summary>
public sealed class HackerNewsSourceProvider : ISourceProvider
{
    public const string ProviderKey = "hackernews";
    private readonly HttpClient _http;
    private readonly HackerNewsOptions _options;
    private readonly ILogger<HackerNewsSourceProvider> _logger;
    private DateTime _lastCallUtc = DateTime.MinValue;

    public HackerNewsSourceProvider(
        HttpClient http,
        IOptions<HackerNewsOptions> options,
        ILogger<HackerNewsSourceProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.Timeout = SourceProviderLimits.HttpTimeout;
    }

    public string ProviderId => ProviderKey;
    public string DisplayName => "Hacker News (public search)";
    public bool RequiresCredentials => false;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public async Task<SourceBatch> SearchAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        var retrievedAt = DateTime.UtcNow;
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://hn.algolia.com/api/v1/"
            : _options.BaseUrl.Trim().TrimEnd('/') + "/";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || !string.Equals(baseUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "invalid_base_url", retrievedAt);
        }

        var queries = BuildQueries(query);
        var candidates = new List<SourceFetchResult>();
        var partial = false;
        string diagnostic = "ok";
        DateTime? retryAfter = null;

        foreach (var q in queries)
        {
            var (batch, ok) = await ExecuteQueryAsync(baseUrl, q, query, cancellationToken).ConfigureAwait(false);
            if (!ok)
            {
                partial = true;
                diagnostic = batch.DiagnosticCode;
                retryAfter ??= batch.RetryAfterUtc;
                foreach (var c in batch.Candidates) candidates.Add(c);
                continue;
            }
            foreach (var c in batch.Candidates) candidates.Add(c);
            if (batch.Status == Domain.SourceRunStatus.Partial)
            {
                partial = true;
                diagnostic = batch.DiagnosticCode;
            }
        }

        var status = candidates.Count == 0 && partial
            ? Domain.SourceRunStatus.Failed
            : partial ? Domain.SourceRunStatus.Partial : Domain.SourceRunStatus.Complete;
        if (status == Domain.SourceRunStatus.Complete) diagnostic = "ok";
        return new SourceBatch(ProviderKey, candidates, status, diagnostic, retrievedAt, retryAfter);
    }

    public Task<SourceFetchResult?> FetchAsync(SourceReference reference, CancellationToken cancellationToken) =>
        Task.FromResult<SourceFetchResult?>(null);

    public static IReadOnlyList<string> BuildQueries(SourceQuery query)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Text)) list.Add(query.Text.Trim());
        foreach (var t in query.Topics.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            if (list.Count >= SourceProviderLimits.MaxQueriesPerRun) break;
            list.Add(t.Trim());
        }
        if (list.Count == 0) list.Add("*");
        return list.Take(SourceProviderLimits.BoundQueries(query.MaxQueries)).ToList();
    }

    private async Task<(SourceBatch Batch, bool Ok)> ExecuteQueryAsync(
        string baseUrl, string text, SourceQuery query, CancellationToken ct)
    {
        var retrievedAt = DateTime.UtcNow;
        var perQuery = SourceProviderLimits.BoundResultsPerQuery(query.MaxResults);
        var url = $"{baseUrl}search?query={Uri.EscapeDataString(text)}&hitsPerPage={perQuery}&page=0";
        var attempt = 0;
        while (true)
        {
            attempt++;
            await EnforceIntervalAsync(ct).ConfigureAwait(false);
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 429)
                {
                    var retryAfter = ParseRetryAfter(response);
                    if (attempt >= SourceProviderLimits.MaxAttempts)
                    {
                        return (SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "rate_limited", retrievedAt, retryAfter), false);
                    }
                    await BoundedDelayAsync(attempt, ct).ConfigureAwait(false);
                    continue;
                }
                if ((int)response.StatusCode >= 500)
                {
                    if (attempt >= SourceProviderLimits.MaxAttempts)
                    {
                        return (SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "provider_5xx", retrievedAt), false);
                    }
                    await BoundedDelayAsync(attempt, ct).ConfigureAwait(false);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                {
                    return (SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, $"provider_http_{(int)response.StatusCode}", retrievedAt), false);
                }
                var body = await ReadBoundedAsync(response, ct).ConfigureAwait(false);
                if (body is null)
                {
                    return (SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "oversized_response", retrievedAt), false);
                }
                var parsed = ParseHits(body, retrievedAt);
                if (parsed is null)
                {
                    return (SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "invalid_payload", retrievedAt), false);
                }
                var status = parsed.Count < perQuery ? Domain.SourceRunStatus.Complete : Domain.SourceRunStatus.Complete;
                return (new SourceBatch(ProviderKey, parsed, status, "ok", retrievedAt), true);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                if (attempt >= SourceProviderLimits.MaxAttempts)
                {
                    return (SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "timeout", retrievedAt), false);
                }
                await BoundedDelayAsync(attempt, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HackerNews provider request failed (attempt {Attempt}).", attempt);
                if (attempt >= SourceProviderLimits.MaxAttempts)
                {
                    return (SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "provider_unavailable", retrievedAt), false);
                }
                await BoundedDelayAsync(attempt, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task EnforceIntervalAsync(CancellationToken ct)
    {
        var elapsed = DateTime.UtcNow - _lastCallUtc;
        if (elapsed < SourceProviderLimits.MinProviderInterval)
        {
            await Task.Delay(SourceProviderLimits.MinProviderInterval - elapsed, ct).ConfigureAwait(false);
        }
        _lastCallUtc = DateTime.UtcNow;
    }

    private static async Task BoundedDelayAsync(int attempt, CancellationToken ct)
    {
        var delay = TimeSpan.FromMilliseconds(Math.Min(200 * Math.Pow(2, attempt - 1), 2000));
        await Task.Delay(delay, ct).ConfigureAwait(false);
    }

    public static DateTime? ParseRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
        {
            return DateTime.UtcNow.Add(delta);
        }
        if (response.Headers.RetryAfter?.Date is DateTimeOffset date)
        {
            return date.UtcDateTime;
        }
        return DateTime.UtcNow.AddSeconds(60);
    }

    public static async Task<string?> ReadBoundedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > SourceProviderLimits.MaxResponseBytes)
        {
            return null;
        }
        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
        {
            ms.Write(buffer, 0, read);
            if (ms.Length > SourceProviderLimits.MaxResponseBytes) return null;
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    public static IReadOnlyList<SourceFetchResult>? ParseHits(string body, DateTime retrievedAt)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var results = new List<SourceFetchResult>();
            foreach (var hit in hits.EnumerateArray())
            {
                var objectId = hit.TryGetProperty("objectID", out var idEl) ? idEl.GetString() : null;
                var title = hit.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null;
                var url = hit.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
                var author = hit.TryGetProperty("author", out var authorEl) ? authorEl.GetString() : null;
                DateTime? published = null;
                if (hit.TryGetProperty("created_at", out var createdEl) && createdEl.ValueKind == JsonValueKind.String)
                {
                    if (DateTime.TryParse(createdEl.GetString(), out var parsed)) published = parsed.ToUniversalTime();
                }
                if (string.IsNullOrWhiteSpace(objectId)) continue;
                var canonical = string.IsNullOrWhiteSpace(url)
                    ? $"https://news.ycombinator.com/item?id={objectId}"
                    : url.Trim();
                if (!CanonicalUrl.TryNormalize(canonical, out var normalized)) continue;
                if (!normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    && !normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var observation = new ProviderObservation(
                    ProviderKey, ProviderStatus.Available, null, null, retrievedAt, 200);
                results.Add(new SourceFetchResult(
                    ProviderKey, $"hn-{objectId}", normalized,
                    string.IsNullOrWhiteSpace(title) ? $"HN discussion {objectId}" : title,
                    null, published, retrievedAt,
                    $"{{\"source\":\"{ProviderKey}\",\"objectID\":\"{objectId}\"}}",
                    observation, author));
                if (results.Count >= SourceProviderLimits.MaxResultsPerQuery) break;
            }
            return results;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

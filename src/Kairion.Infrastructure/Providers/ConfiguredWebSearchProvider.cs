using System.Net;
using System.Text.Json;
using Kairion.Application.Abstractions;
using Kairion.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kairion.Infrastructure.Providers;

/// <summary>
/// Configured web-search adapter. The endpoint is owner-configured (HTTPS only)
/// and the credential lives in deployment secret storage (environment /
/// configuration), never in PostgreSQL or API responses. Disabled by default
/// until a valid HTTPS endpoint is configured. Uses only documented API
/// responses — no HTML scraping or access-control bypass.
/// </summary>
public sealed class ConfiguredWebSearchProvider : ISourceProvider
{
    public const string ProviderKey = "web-search";
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfiguredWebSearchProvider> _logger;

    public ConfiguredWebSearchProvider(
        HttpClient http,
        IConfiguration configuration,
        ILogger<ConfiguredWebSearchProvider> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
        _http.Timeout = SourceProviderLimits.HttpTimeout;
    }

    public string ProviderId => ProviderKey;
    public string DisplayName => "Web search (configured endpoint)";
    public bool RequiresCredentials => true;

    public string? ConfiguredEndpoint => _configuration["Kairion:SourceProviders:WebSearch:Endpoint"];

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        Task.FromResult(IsConfigured());

    public async Task<SourceBatch> SearchAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        var retrievedAt = DateTime.UtcNow;
        var endpoint = ConfiguredEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Disabled, "not_configured", retrievedAt);
        }
        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "invalid_endpoint", retrievedAt);
        }

        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                var perQuery = SourceProviderLimits.BoundResultsPerQuery(query.MaxResults);
                var url = $"{endpoint.Trim().TrimEnd('/')}?q={Uri.EscapeDataString(query.Text ?? string.Empty)}&count={perQuery}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                var apiKey = _configuration["Kairion:SourceProviders:WebSearch:ApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");
                }
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = HackerNewsSourceProvider.ParseRetryAfter(response);
                    if (attempt >= SourceProviderLimits.MaxAttempts)
                    {
                        return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "rate_limited", retrievedAt, retryAfter);
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(200 * Math.Pow(2, attempt - 1), 2000)), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if ((int)response.StatusCode >= 500)
                {
                    if (attempt >= SourceProviderLimits.MaxAttempts)
                    {
                        return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "provider_5xx", retrievedAt);
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(200 * Math.Pow(2, attempt - 1), 2000)), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "unauthorized", retrievedAt);
                }
                if (!response.IsSuccessStatusCode)
                {
                    return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, $"provider_http_{(int)response.StatusCode}", retrievedAt);
                }
                var body = await HackerNewsSourceProvider.ReadBoundedAsync(response, cancellationToken).ConfigureAwait(false);
                if (body is null)
                {
                    return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "oversized_response", retrievedAt);
                }
                var parsed = ParseResults(body, retrievedAt);
                if (parsed is null)
                {
                    return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "invalid_payload", retrievedAt);
                }
                return new SourceBatch(ProviderKey, parsed.Take(perQuery).ToList(), Domain.SourceRunStatus.Complete, "ok", retrievedAt);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt >= SourceProviderLimits.MaxAttempts)
                {
                    return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "timeout", retrievedAt);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Web-search provider request failed (attempt {Attempt}).", attempt);
                if (attempt >= SourceProviderLimits.MaxAttempts)
                {
                    return SourceBatch.Empty(ProviderKey, Domain.SourceRunStatus.Failed, "provider_unavailable", retrievedAt);
                }
            }
        }
    }

    public Task<SourceFetchResult?> FetchAsync(SourceReference reference, CancellationToken cancellationToken) =>
        Task.FromResult<SourceFetchResult?>(null);

    private bool IsConfigured()
    {
        var endpoint = ConfiguredEndpoint;
        return !string.IsNullOrWhiteSpace(endpoint)
            && Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<SourceFetchResult>? ParseResults(string body, DateTime retrievedAt)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            JsonElement items;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                items = doc.RootElement;
            }
            else if (doc.RootElement.TryGetProperty("results", out var r) && r.ValueKind == JsonValueKind.Array)
            {
                items = r;
            }
            else if (doc.RootElement.TryGetProperty("items", out var it) && it.ValueKind == JsonValueKind.Array)
            {
                items = it;
            }
            else
            {
                return null;
            }
            var results = new List<SourceFetchResult>();
            foreach (var item in items.EnumerateArray())
            {
                var url = GetString(item, "url") ?? GetString(item, "link");
                var title = GetString(item, "title") ?? GetString(item, "name");
                var excerpt = GetString(item, "excerpt") ?? GetString(item, "snippet") ?? GetString(item, "description");
                var id = GetString(item, "id") ?? url;
                DateTime? published = null;
                var publishedRaw = GetString(item, "publishedAt") ?? GetString(item, "published_at") ?? GetString(item, "date");
                if (!string.IsNullOrWhiteSpace(publishedRaw) && DateTime.TryParse(publishedRaw, out var parsed))
                {
                    published = parsed.ToUniversalTime();
                }
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(id)) continue;
                if (!CanonicalUrl.TryNormalize(url, out var normalized)) continue;
                if (!normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;
                var observation = new ProviderObservation(
                    ProviderKey, ProviderStatus.Available, null, null, retrievedAt, 200);
                results.Add(new SourceFetchResult(
                    ProviderKey, $"web-{Math.Abs(id.GetHashCode()):x}-{Math.Abs(normalized.GetHashCode()):x}",
                    normalized, title, excerpt, published, retrievedAt,
                    $"{{\"source\":\"{ProviderKey}\"}}", observation));
                if (results.Count >= SourceProviderLimits.MaxResultsPerQuery) break;
            }
            return results;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }
}

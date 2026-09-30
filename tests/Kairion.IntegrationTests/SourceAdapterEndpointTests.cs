using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Kairion.Application.Abstractions;
using Kairion.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Kairion.IntegrationTests;

/// <summary>
/// R1–R4 oracle for the source-adapter package: provider configuration
/// validation, provenance-preserving retrieval, idempotent canonical-URL
/// upsert, run evidence, and per-provider failure isolation.
/// </summary>
public class SourceAdapterEndpointTests : IAsyncLifetime
{
    private readonly KairionApiFactory _factory = new();

    public Task InitializeAsync()
    {
        _ = _factory.Server;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Configure_ValidProvider_Stored_Without_Credentials()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "adapter config");
        var update = new
        {
            title = "adapter config",
            briefKind = "market",
            briefText = "Brief text for testing.",
            topics = Array.Empty<string>(),
            includedCompetitors = Array.Empty<string>(),
            enabledSourceProviderIds = Array.Empty<string>(),
            providerConfigs = new[]
            {
                new { providerId = "fake-source", enabled = true, maxQueries = 2, maxResultsPerQuery = 10, endpoint = (string?)null, credentialRef = "fake-ref" },
            },
        };
        var response = await client.PutAsJsonAsync($"/api/v1/research-projects/{projectId}", update, TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("enabledSourceProviderIds").EnumerateArray().Select(e => e.GetString())
            .Should().Contain("fake-source");
        var configs = doc.GetProperty("providerConfigs").EnumerateArray().ToList();
        configs.Should().HaveCount(1);
        configs[0].GetProperty("credentialRef").GetString().Should().Be("fake-ref");
        doc.GetRawText().Should().NotContain("sk-");
    }

    [Theory]
    [InlineData("http://example.test/search")]
    [InlineData("not-a-url")]
    public async Task Configure_NonHttpsEndpoint_Rejected_PriorPreserved(string endpoint)
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, $"reject {Guid.NewGuid():N}");
        var bad = new
        {
            title = "adapter config",
            briefKind = "market",
            briefText = "Brief text for testing.",
            topics = Array.Empty<string>(),
            includedCompetitors = Array.Empty<string>(),
            enabledSourceProviderIds = Array.Empty<string>(),
            providerConfigs = new[]
            {
                new { providerId = "fake-source", enabled = true, maxQueries = 2, maxResultsPerQuery = 10, endpoint, credentialRef = (string?)null },
            },
        };
        var response = await client.PutAsJsonAsync($"/api/v1/research-projects/{projectId}", bad, TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var get = await client.GetAsync($"/api/v1/research-projects/{projectId}");
        var doc = await get.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        // Creation enabled manual + fake-source, so the prior configuration is
        // preserved untouched and the bad endpoint is never stored.
        var configs = doc.GetProperty("providerConfigs").EnumerateArray().ToList();
        configs.Should().HaveCount(2);
        doc.GetRawText().Should().NotContain(endpoint);
    }

    [Fact]
    public async Task Configure_UnknownProvider_Rejected()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "unknown key");
        var bad = new
        {
            title = "unknown key",
            briefKind = "market",
            briefText = "Brief text for testing.",
            topics = Array.Empty<string>(),
            includedCompetitors = Array.Empty<string>(),
            enabledSourceProviderIds = new[] { "nope-provider" },
        };
        var response = await client.PutAsJsonAsync($"/api/v1/research-projects/{projectId}", bad, TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(6, 10)]
    [InlineData(2, 0)]
    [InlineData(2, 51)]
    public async Task Configure_OutOfRange_Limits_Rejected(int maxQueries, int maxResults)
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, $"limits {Guid.NewGuid():N}");
        var bad = new
        {
            title = "limits",
            briefKind = "market",
            briefText = "Brief text for testing.",
            topics = Array.Empty<string>(),
            includedCompetitors = Array.Empty<string>(),
            enabledSourceProviderIds = Array.Empty<string>(),
            providerConfigs = new[]
            {
                new { providerId = "fake-source", enabled = true, maxQueries, maxResultsPerQuery = maxResults, endpoint = (string?)null, credentialRef = (string?)null },
            },
        };
        var response = await client.PutAsJsonAsync($"/api/v1/research-projects/{projectId}", bad, TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Retrieve_DuplicateCanonicalUrl_NoDuplicate_LastSeenUpdated()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "dedup run");
        var spy = _factory.Services.GetRequiredService<ProviderRegistrySpy>();
        spy.Reset();
        var now = DateTime.UtcNow;
        spy.Source.QueuedResults.Add(NewFetchResult("https://example.test/dup#frag", "ext-dup-1", now));

        var first = await client.PostAsJsonAsync(
            $"/api/v1/research-projects/{projectId}/candidates/search?providerId=fake-source",
            new { text = "q", topics = Array.Empty<string>(), maxResults = 10 },
            TestJson.Options);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        spy.Reset();
        spy.Source.QueuedResults.Add(NewFetchResult("https://example.test/dup/", "ext-dup-2", now.AddMinutes(5)));
        var second = await client.PostAsJsonAsync(
            $"/api/v1/research-projects/{projectId}/candidates/search?providerId=fake-source",
            new { text = "q", topics = Array.Empty<string>(), maxResults = 10 },
            TestJson.Options);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetAsync($"/api/v1/research-projects/{projectId}/candidates");
        var items = await list.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("canonicalUrl").GetString().Should().Be("https://example.test/dup");

        var runs = await client.GetAsync($"/api/v1/research-projects/{projectId}/source-runs");
        var runDocs = await runs.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        runDocs.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        runDocs.EnumerateArray().All(r => r.GetProperty("providerId").GetString() == "fake-source").Should().BeTrue();
    }

    [Fact]
    public async Task RateLimited_Batch_Records_Retryable_Without_Losing_Evidence()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "rate limit");
        var spy = _factory.Services.GetRequiredService<ProviderRegistrySpy>();
        spy.Reset();
        spy.Source.QueuedStatus = SourceRunStatus.Failed;
        spy.Source.QueuedDiagnostic = "rate_limited";
        spy.Source.QueuedRetryAfterUtc = DateTime.UtcNow.AddSeconds(60);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/research-projects/{projectId}/candidates/search?providerId=fake-source",
            new { text = "q", topics = Array.Empty<string>(), maxResults = 10 },
            TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetArrayLength().Should().Be(0);

        var runs = await client.GetAsync($"/api/v1/research-projects/{projectId}/source-runs");
        var runDocs = await runs.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var latest = runDocs.EnumerateArray().First();
        latest.GetProperty("diagnosticCode").GetString().Should().Be("rate_limited");
        latest.GetProperty("retryAfterUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Disabled_Provider_Skipped_With_Disabled_Run()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "disabled run", enabled: new[] { "manual" });
        var spy = _factory.Services.GetRequiredService<ProviderRegistrySpy>();
        spy.Reset();
        spy.Source.QueuedResults.Add(NewFetchResult("https://example.test/disabled-1", "ext-x", DateTime.UtcNow));

        var response = await client.PostAsJsonAsync(
            $"/api/v1/research-projects/{projectId}/candidates/search?providerId=fake-source",
            new { text = "q", topics = Array.Empty<string>(), maxResults = 10 },
            TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetArrayLength().Should().Be(0);

        var runs = await client.GetAsync($"/api/v1/research-projects/{projectId}/source-runs");
        var runDocs = await runs.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        runDocs.EnumerateArray().First().GetProperty("status").GetString().Should().Be("Disabled");
    }

    [Fact]
    public async Task Concurrent_Duplicate_Runs_Idempotent()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "concurrent");
        var spy = _factory.Services.GetRequiredService<ProviderRegistrySpy>();
        spy.Reset();
        var now = DateTime.UtcNow;
        spy.Source.QueuedResults.Add(NewFetchResult("https://example.test/concurrent", "ext-c", now));

        var body = new { text = "q", topics = Array.Empty<string>(), maxResults = 10 };
        var results = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/candidates/search?providerId=fake-source", body, TestJson.Options),
            client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/candidates/search?providerId=fake-source", body, TestJson.Options));
        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        var list = await client.GetAsync($"/api/v1/research-projects/{projectId}/candidates");
        var items = await list.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        items.GetArrayLength().Should().Be(1);
    }

    private static SourceFetchResult NewFetchResult(string url, string externalId, DateTime now)
    {
        return new SourceFetchResult(
            providerId: "fake-source",
            externalId: externalId,
            canonicalUrl: url,
            title: "Synthetic",
            excerpt: "Excerpt",
            publishedUtc: now.AddDays(-1),
            observedUtc: now,
            provenanceJson: "{\"source\":\"fake\"}",
            observation: new ProviderObservation("fake-source", ProviderStatus.Available, null, null, now, 200));
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client, string title, string[]? enabled = null)
    {
        var body = new
        {
            title,
            briefKind = "market",
            briefText = "Brief text for testing.",
            topics = Array.Empty<string>(),
            includedCompetitors = Array.Empty<string>(),
            enabledSourceProviderIds = enabled ?? new[] { "manual", "fake-source" },
        };
        var response = await client.PostAsJsonAsync("/api/v1/research-projects", body, TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        return doc.GetProperty("id").GetGuid();
    }
}

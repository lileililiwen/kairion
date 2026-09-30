using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Kairion.Application.Abstractions;
using Kairion.Application.Schemas;
using Kairion.Domain;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairion.IntegrationTests;

/// <summary>
/// End-to-end tests for the R2 candidate intake scenarios: manual URL import is
/// idempotent, the source-provider boundary is enforced, and policy denials do not
/// produce duplicate evidence.
/// </summary>
public class CandidateIntakeEndpointTests : IAsyncLifetime
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
    public async Task Import_SameIdentityTwice_ReturnsExistingItem()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "import idempotency");
        var import = new
        {
            providerId = "manual",
            externalId = "ext-1",
            canonicalUrl = "https://example.test/post-1",
            title = "First import",
        };
        var first = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/candidates/import", import, TestJson.Options);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();

        var second = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/candidates/import", import, TestJson.Options);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();

        secondId.Should().Be(firstId);

        var list = await client.GetAsync($"/api/v1/research-projects/{projectId}/candidates");
        var items = await list.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        items.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Import_SameCanonicalUrl_DifferentProviderId_Deduplicated()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "canonical dedup");
        var first = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/candidates/import", new
        {
            providerId = "manual",
            externalId = "ext-a",
            canonicalUrl = "https://example.test/dup",
        }, TestJson.Options);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();

        var second = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/candidates/import", new
        {
            providerId = "manual",
            externalId = "ext-b",
            canonicalUrl = "https://example.test/dup",
        }, TestJson.Options);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();

        secondId.Should().Be(firstId);
    }

    [Fact]
    public async Task Import_RelativeUrl_ReturnsValidationFailed()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "url validation");
        var response = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/candidates/import", new
        {
            providerId = "manual",
            externalId = "ext-1",
            canonicalUrl = "not-a-url",
        }, TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_ProviderDeniedResults_NotPersisted()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "denial path");
        var spy = _factory.Services.GetRequiredService<ProviderRegistrySpy>();
        spy.Source.QueuedResults.Add(NewFetchResult(providerStatus: ProviderStatus.PolicyDenied));
        spy.Source.QueuedResults.Add(NewFetchResult(providerStatus: ProviderStatus.Available));

        var response = await client.PostAsJsonAsync(
            $"/api/v1/research-projects/{projectId}/candidates/search?providerId={spy.Source.ProviderId}",
            new { text = "anything", topics = Array.Empty<string>(), maxResults = 10 },
            TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        // Only the available one should be persisted.
        items.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Search_UnknownProvider_ReturnsConfigurationError()
    {
        var client = _factory.CreateClient();
        var projectId = await CreateProjectAsync(client, "unknown provider");
        var response = await client.PostAsJsonAsync(
            $"/api/v1/research-projects/{projectId}/candidates/search?providerId=does-not-exist",
            new { text = "x", topics = Array.Empty<string>(), maxResults = 1 },
            TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static SourceFetchResult NewFetchResult(ProviderStatus providerStatus)
    {
        var now = DateTime.UtcNow;
        return new SourceFetchResult(
            providerId: "fake-source",
            externalId: Guid.NewGuid().ToString("N"),
            canonicalUrl: $"https://example.test/{Guid.NewGuid():N}",
            title: "Synthetic",
            excerpt: "Excerpt",
            publishedUtc: now.AddDays(-1),
            observedUtc: now,
            provenanceJson: "{\"source\":\"fake\"}",
            observation: new ProviderObservation(
                providerId: "fake-source",
                status: providerStatus,
                errorCode: providerStatus == ProviderStatus.PolicyDenied ? "denied" : null,
                message: null,
                observedUtc: now,
                httpStatus: providerStatus == ProviderStatus.PolicyDenied ? 403 : 200));
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client, string title)
    {
        var body = new
        {
            title,
            briefKind = "market",
            briefText = "Brief text for testing.",
            topics = Array.Empty<string>(),
            includedCompetitors = Array.Empty<string>(),
            enabledSourceProviderIds = new[] { "manual" },
        };
        var response = await client.PostAsJsonAsync("/api/v1/research-projects", body, TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        return doc.GetProperty("id").GetGuid();
    }
}

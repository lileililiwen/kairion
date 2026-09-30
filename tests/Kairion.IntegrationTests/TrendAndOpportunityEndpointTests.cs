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
using Kairion.Domain;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairion.IntegrationTests;

/// <summary>
/// End-to-end tests for R5 deterministic trends and R6 opportunity signals. The trends
/// oracle must report zero-baseline as null + new_signal=true; the opportunity signal must
/// disclose its evidence, label the disclosure, and never present a positive signal when
/// the cluster has no retained evidence.
/// </summary>
public class TrendAndOpportunityEndpointTests : IAsyncLifetime
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
    public async Task Trends_BothWindowsEmpty_ReturnsNoBaseline()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "trend empty");

        var response = await client.GetAsync($"/api/v1/research-projects/{projectId}/trends?window=30d");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("currentCount").GetInt32().Should().Be(0);
        doc.GetProperty("previousCount").GetInt32().Should().Be(0);
        doc.TryGetProperty("percentGrowth", out var percent).Should().BeTrue();
        percent.ValueKind.Should().Be(JsonValueKind.Null);
        doc.GetProperty("newSignal").GetBoolean().Should().BeFalse();
        doc.GetProperty("label").GetString().Should().Be("no_baseline");
    }

    [Fact]
    public async Task Trends_WithCurrentItemsNoPrevious_ReturnsNewSignal()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "trend new signal");
        await SeedObservationsAsync(projectId, currentWindow: 2, previousWindow: 0);

        var response = await client.GetAsync($"/api/v1/research-projects/{projectId}/trends?window=30d");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("currentCount").GetInt32().Should().Be(2);
        doc.GetProperty("previousCount").GetInt32().Should().Be(0);
        doc.TryGetProperty("percentGrowth", out var percent).Should().BeTrue();
        percent.ValueKind.Should().Be(JsonValueKind.Null);
        doc.GetProperty("newSignal").GetBoolean().Should().BeTrue();
        doc.GetProperty("label").GetString().Should().Be("new_signal");
    }

    [Fact]
    public async Task Trends_BothWindowsHaveData_ReturnsPercent()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "trend baseline");
        await SeedObservationsAsync(projectId, currentWindow: 4, previousWindow: 2);

        var response = await client.GetAsync($"/api/v1/research-projects/{projectId}/trends?window=30d");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("currentCount").GetInt32().Should().Be(4);
        doc.GetProperty("previousCount").GetInt32().Should().Be(2);
        doc.GetProperty("percentGrowth").GetDecimal().Should().Be(1.0m);
        doc.GetProperty("newSignal").GetBoolean().Should().BeFalse();
        doc.GetProperty("label").GetString().Should().Be("emerging");
    }

    [Fact]
    public async Task Trends_UnknownWindow_ReturnsValidationFailed()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "trend bad window");
        var response = await client.GetAsync($"/api/v1/research-projects/{projectId}/trends?window=14d");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("code").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task Opportunity_AlwaysIncludesDisclosure()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "opp disclosure");
        var clusterId = await CreateClusterAsync(client, projectId);
        var response = await client.GetAsync($"/api/v1/research-projects/{projectId}/opportunity-signals/{clusterId}?window=30d");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("disclosure").GetString().Should().Be("This is an evidence signal, not business validation.");
        doc.GetProperty("trendLabel").GetString().Should().Be("no_evidence");
        doc.GetProperty("evidenceVolume").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Opportunity_WithoutEvidence_DoesNotPresentPositiveSignal()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "opp empty");
        var clusterId = await CreateClusterAsync(client, projectId);
        var response = await client.GetAsync($"/api/v1/research-projects/{projectId}/opportunity-signals/{clusterId}?window=30d");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("aiExplanation").GetString().Should().Contain("No retained");
    }

    private static async Task<Guid> SeedProjectAsync(HttpClient client, string title)
    {
        var body = new
        {
            title,
            briefKind = "market",
            briefText = "brief",
            topics = Array.Empty<string>(),
            includedCompetitors = Array.Empty<string>(),
            enabledSourceProviderIds = new[] { "manual" },
        };
        var post = await client.PostAsJsonAsync("/api/v1/research-projects", body, TestJson.Options);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateClusterAsync(HttpClient client, Guid projectId)
    {
        var post = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/clusters", new
        {
            label = "Cluster",
            category = "cat",
            summary = "summary",
        }, TestJson.Options);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Writes synthetic observations directly via the DbContext so the trend / opportunity
    /// services have something to count. Keeps the test independent of the source-intake
    /// pipeline.
    /// </summary>
    private async Task SeedObservationsAsync(Guid projectId, int currentWindow, int previousWindow)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Kairion.Infrastructure.Persistence.KairionDbContext>();
        var now = DateTime.UtcNow;
        for (var i = 0; i < currentWindow; i++)
        {
            db.Observations.Add(new Observation(
                id: Guid.NewGuid(),
                projectId: projectId,
                sourceItemId: Guid.NewGuid(),
                clusterId: null,
                observedUtc: now.AddDays(-i - 1),
                firstObservedUtc: now.AddDays(-i - 1)));
        }
        for (var i = 0; i < previousWindow; i++)
        {
            db.Observations.Add(new Observation(
                id: Guid.NewGuid(),
                projectId: projectId,
                sourceItemId: Guid.NewGuid(),
                clusterId: null,
                observedUtc: now.AddDays(-30 - i - 1),
                firstObservedUtc: now.AddDays(-30 - i - 1)));
        }
        await db.SaveChangesAsync();
    }
}

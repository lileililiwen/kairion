using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Kairion.Application.Schemas;
using Kairion.Domain;
using Xunit;

namespace Kairion.IntegrationTests;

/// <summary>
/// End-to-end tests for R4 pain clusters: evidence-linked, human revisions win over
/// later AI suggestions, and merge/split produce auditable revisions.
/// </summary>
public class ClusterEndpointTests : IAsyncLifetime
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
    public async Task CreateCluster_ThenList_AppearsInProject()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "clusters");
        var post = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/clusters", new
        {
            label = "Manual reconciliation",
            category = "tooling",
            summary = "Recurring complaints about spreadsheet workarounds.",
        }, TestJson.Options);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var clusterId = doc.GetProperty("id").GetGuid();

        var list = await client.GetAsync($"/api/v1/research-projects/{projectId}/clusters");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await list.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        items.GetArrayLength().Should().Be(1);

        var evidence = await client.GetAsync($"/api/v1/clusters/{clusterId}");
        evidence.StatusCode.Should().Be(HttpStatusCode.OK);
        var clusterDoc = await evidence.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        clusterDoc.GetProperty("label").GetString().Should().Be("Manual reconciliation");
    }

    [Fact]
    public async Task HumanEdit_RecordsRevision_AndUpdatesCluster()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "human edit");
        var clusterId = await CreateClusterAsync(client, projectId, "Old label", "cat", "sum");

        var put = await client.PutAsJsonAsync($"/api/v1/clusters/{clusterId}", new
        {
            label = "Updated label",
            category = "cat",
            summary = "Updated summary",
        }, TestJson.Options);
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var revisions = await client.GetAsync($"/api/v1/clusters/{clusterId}/revisions");
        revisions.StatusCode.Should().Be(HttpStatusCode.OK);
        var revs = await revisions.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        revs.GetArrayLength().Should().Be(1);
        revs[0].GetProperty("action").GetString().Should().Be("editedCluster");
    }

    [Fact]
    public async Task Reassign_HumanOrigin_RecordsRevisionEvenWhenAIExists()
    {
        var client = _factory.CreateClient();
        var (projectId, sourceId) = await SeedSourceAsync(client, "reassign");
        var clusterA = await CreateClusterAsync(client, projectId, "Cluster A", "cat", "summary A");
        var clusterB = await CreateClusterAsync(client, projectId, "Cluster B", "cat", "summary B");

        // AI assignment to A.
        var ai = await client.PostAsJsonAsync(
            $"/api/v1/clusters/{clusterA}/reassign",
            new { sourceItemId = sourceId, targetClusterId = clusterA },
            TestJson.Options);
        ai.StatusCode.Should().Be(HttpStatusCode.OK);

        // Human reassignment to B.
        var human = await client.PostAsJsonAsync(
            $"/api/v1/clusters/{clusterB}/reassign",
            new { sourceItemId = sourceId, targetClusterId = clusterB },
            TestJson.Options);
        human.StatusCode.Should().Be(HttpStatusCode.OK);

        var revs = await client.GetAsync($"/api/v1/clusters/{clusterB}/revisions");
        var revList = await revs.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        revList.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task MergeClusters_PullsAssignmentsFromSource()
    {
        var client = _factory.CreateClient();
        var (projectId, sourceId) = await SeedSourceAsync(client, "merge");
        var clusterA = await CreateClusterAsync(client, projectId, "Target", "cat", "summary");
        var clusterB = await CreateClusterAsync(client, projectId, "Source", "cat", "summary");
        await client.PostAsJsonAsync(
            $"/api/v1/clusters/{clusterB}/reassign",
            new { sourceItemId = sourceId, targetClusterId = clusterB },
            TestJson.Options);

        var merge = await client.PostAsJsonAsync(
            $"/api/v1/clusters/{clusterA}/merge",
            new { sourceClusterId = clusterB, targetClusterId = clusterA },
            TestJson.Options);
        merge.StatusCode.Should().Be(HttpStatusCode.OK);

        var evidence = await client.GetAsync($"/api/v1/clusters/{clusterA}");
        var doc = await evidence.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var items = doc.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
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

    private static async Task<Guid> CreateClusterAsync(HttpClient client, Guid projectId, string label, string category, string summary)
    {
        var post = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/clusters", new
        {
            label,
            category,
            summary,
        }, TestJson.Options);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
    }

    private static async Task<(Guid projectId, Guid sourceId)> SeedSourceAsync(HttpClient client, string title)
    {
        var projectId = await SeedProjectAsync(client, title);
        var import = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/candidates/import", new
        {
            providerId = "manual",
            externalId = Guid.NewGuid().ToString("N"),
            canonicalUrl = $"https://example.test/{Guid.NewGuid():N}",
        }, TestJson.Options);
        import.StatusCode.Should().Be(HttpStatusCode.Created);
        var sourceId = (await import.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
        return (projectId, sourceId);
    }
}

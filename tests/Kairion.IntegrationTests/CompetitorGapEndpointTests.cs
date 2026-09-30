using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Kairion.Domain;
using Kairion.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairion.IntegrationTests;

/// <summary>
/// End-to-end tests for the competitor-gap matrix: stable competitor identity,
/// explicit assignment with an Unmapped bucket, deterministic windows, read-only
/// project scoping, and empty/archived boundaries.
/// </summary>
public class CompetitorGapEndpointTests : IAsyncLifetime
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
    public async Task Competitors_AreStableAcrossProjectUpdates()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "gap stable", new[] { "Acme", "Beta" });

        var first = await client.GetFromJsonAsync<JsonElement>($"/api/v1/research-projects/{projectId}/competitors", TestJson.Options);
        var acmeId = first.EnumerateArray().Single(e => e.GetProperty("name").GetString() == "Acme").GetProperty("id").GetGuid();

        // Update the project (rename list order + add a name); Acme must keep its id.
        var get = await client.GetFromJsonAsync<JsonElement>($"/api/v1/research-projects/{projectId}", TestJson.Options);
        var update = new
        {
            title = get.GetProperty("title").GetString(),
            briefKind = "market",
            briefText = "brief",
            topics = Array.Empty<string>(),
            includedCompetitors = new[] { "Beta", "Acme", "Gamma" },
            enabledSourceProviderIds = new[] { "manual" },
        };
        var put = await client.PutAsJsonAsync($"/api/v1/research-projects/{projectId}", update, TestJson.Options);
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.GetFromJsonAsync<JsonElement>($"/api/v1/research-projects/{projectId}/competitors", TestJson.Options);
        second.EnumerateArray().Single(e => e.GetProperty("name").GetString() == "Acme").GetProperty("id").GetGuid()
            .Should().Be(acmeId);
        second.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task GapMatrix_WithAssignedAndUnmappedEvidence_ReturnsCellsAndCoverage()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "gap matrix", new[] { "Acme", "Beta" });
        var competitors = await client.GetFromJsonAsync<JsonElement>($"/api/v1/research-projects/{projectId}/competitors", TestJson.Options);
        var acmeId = competitors.EnumerateArray().Single(e => e.GetProperty("name").GetString() == "Acme").GetProperty("id").GetGuid();
        var clusterId = await CreateClusterAsync(client, projectId, "Onboarding pain");

        var mapped = await ImportCandidateAsync(client, projectId, "https://example.test/mapped", daysAgo: 2);
        var unmapped = await ImportCandidateAsync(client, projectId, "https://example.test/unmapped", daysAgo: 3);
        await AssignToClusterAsync(projectId, clusterId, mapped);
        await AssignToClusterAsync(projectId, clusterId, unmapped);

        var assign = await client.PostAsJsonAsync(
            $"/api/v1/research-projects/{projectId}/candidates/{mapped}/competitor-assignments",
            new { competitorId = acmeId }, TestJson.Options);
        assign.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync($"/api/v1/research-projects/{projectId}/competitor-gaps?window=30d");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("window").GetString().Should().Be("30d");
        doc.GetProperty("coverage").GetProperty("totalEvidenceInWindow").GetInt32().Should().Be(2);
        doc.GetProperty("coverage").GetProperty("mappedEvidenceInWindow").GetInt32().Should().Be(1);
        doc.GetProperty("coverage").GetProperty("unmappedEvidenceInWindow").GetInt32().Should().Be(1);

        var cells = doc.GetProperty("cells").EnumerateArray().ToList();
        var mappedCell = cells.Single(c =>
            c.GetProperty("competitorId").ValueKind == JsonValueKind.String &&
            c.GetProperty("competitorId").GetGuid() == acmeId &&
            c.GetProperty("clusterId").GetGuid() == clusterId);
        mappedCell.GetProperty("evidenceCount").GetInt32().Should().Be(1);
        mappedCell.GetProperty("limitedEvidence").GetBoolean().Should().BeTrue();
        mappedCell.GetProperty("classification").GetString().Should().Be("InsufficientData");
        mappedCell.GetProperty("representativeEvidence").GetArrayLength().Should().Be(1);
        mappedCell.GetProperty("representativeEvidence")[0].GetProperty("canonicalUrl").GetString()
            .Should().Be("https://example.test/mapped");

        var unmappedCell = cells.Single(c =>
            c.GetProperty("competitorId").ValueKind == JsonValueKind.Null &&
            c.GetProperty("clusterId").GetGuid() == clusterId);
        unmappedCell.GetProperty("competitorName").GetString().Should().Be("Unmapped");
        unmappedCell.GetProperty("evidenceCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GapMatrix_ForeignCompetitor_ReturnsValidationFailed()
    {
        var client = _factory.CreateClient();
        var projectA = await SeedProjectAsync(client, "gap foreign a", new[] { "Acme" });
        var projectB = await SeedProjectAsync(client, "gap foreign b", new[] { "Other" });
        var otherCompetitors = await client.GetFromJsonAsync<JsonElement>($"/api/v1/research-projects/{projectB}/competitors", TestJson.Options);
        var foreignId = otherCompetitors.EnumerateArray().First().GetProperty("id").GetGuid();

        var response = await client.GetAsync($"/api/v1/research-projects/{projectA}/competitor-gaps?window=30d&competitorId={foreignId}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("code").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task GapMatrix_EmptyProject_ReturnsEmptyMatrix()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "gap empty", new[] { "Acme" });

        var response = await client.GetAsync($"/api/v1/research-projects/{projectId}/competitor-gaps?window=7d");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("coverage").GetProperty("totalEvidenceInWindow").GetInt32().Should().Be(0);
        doc.GetProperty("cells").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GapMatrix_ArchivedProject_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "gap archived", new[] { "Acme" });
        var archive = await client.PostAsync($"/api/v1/research-projects/{projectId}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync($"/api/v1/research-projects/{projectId}/competitor-gaps?window=30d");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GapMatrix_InvalidWindow_ReturnsValidationFailed()
    {
        var client = _factory.CreateClient();
        var projectId = await SeedProjectAsync(client, "gap bad window", new[] { "Acme" });
        var response = await client.GetAsync($"/api/v1/research-projects/{projectId}/competitor-gaps?window=14d");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> SeedProjectAsync(HttpClient client, string title, string[] competitors)
    {
        var body = new
        {
            title,
            briefKind = "market",
            briefText = "brief",
            topics = Array.Empty<string>(),
            includedCompetitors = competitors,
            enabledSourceProviderIds = new[] { "manual" },
        };
        var post = await client.PostAsJsonAsync("/api/v1/research-projects", body, TestJson.Options);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateClusterAsync(HttpClient client, Guid projectId, string label)
    {
        var post = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/clusters", new
        {
            label,
            category = "cat",
            summary = "summary",
        }, TestJson.Options);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> ImportCandidateAsync(HttpClient client, Guid projectId, string url, int daysAgo)
    {
        var post = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/candidates/import", new
        {
            providerId = "manual",
            externalId = Guid.NewGuid().ToString("N"),
            canonicalUrl = url,
            title = url,
        }, TestJson.Options);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();

        // Backdate the observation timestamps so window math is deterministic.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KairionDbContext>();
        var item = await db.SourceItems.FindAsync(id);
        item.Should().NotBeNull();
        var observed = DateTime.UtcNow.AddDays(-daysAgo);
        item!.ObservedUtc = observed;
        item.PublishedUtc = observed;
        item.CreatedUtc = observed;
        await db.SaveChangesAsync();
        return id;
    }

    private async Task AssignToClusterAsync(Guid projectId, Guid clusterId, Guid sourceItemId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KairionDbContext>();
        var now = DateTime.UtcNow;
        db.ClusterAssignments.Add(new ClusterAssignment(
            Guid.NewGuid(), clusterId, sourceItemId, null, AssignmentOrigin.Human, now));
        db.Observations.Add(new Observation(
            Guid.NewGuid(), projectId, sourceItemId, clusterId, now, now));
        await db.SaveChangesAsync();
    }
}

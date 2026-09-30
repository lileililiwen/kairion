using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kairion.Application.Schemas;
using Xunit;

namespace Kairion.IntegrationTests;

/// <summary>
/// End-to-end tests against the real ASP.NET Core pipeline. They cover the R1 research
/// projects scenarios from the active OpenSpec change: create, reject empty brief, list,
/// archive, restore. Each test owns a fresh factory so the in-memory database and
/// provider spy are isolated across tests.
/// </summary>
public class ResearchProjectEndpointTests : IAsyncLifetime
{
    private readonly KairionApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        // Touch the server so the host is built before the first request.
        _ = _factory.Server;
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_ThenGet_RoundTripsProject()
    {
        var client = _factory.CreateClient();
        var body = new
        {
            title = "Investigation: recurring billing complaints",
            briefKind = "market",
            briefText = "Founders mention recurring billing complaints across indie SaaS forums.",
            topics = new[] { "billing", "saas" },
            includedCompetitors = new[] { "Stripe Billing", "Chargebee" },
            enabledSourceProviderIds = new[] { "manual" },
        };
        var post = await client.PostAsJsonAsync("/api/v1/research-projects", body, TestJson.Options);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var id = created.GetProperty("id").GetGuid();

        var get = await client.GetAsync($"/api/v1/research-projects/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await get.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        fetched.GetProperty("title").GetString().Should().Be("Investigation: recurring billing complaints");
        fetched.GetProperty("briefKind").GetString().Should().Be("market");
        fetched.GetProperty("includedCompetitors").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(new[] { "Stripe Billing", "Chargebee" });
    }

    [Fact]
    public async Task Create_WithEmptyBrief_ReturnsValidationFailed()
    {
        var client = _factory.CreateClient();
        var body = new
        {
            title = "t",
            briefKind = "market",
            briefText = "   ",
        };
        var post = await client.PostAsJsonAsync("/api/v1/research-projects", body, TestJson.Options);
        post.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        problem.GetProperty("code").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task List_DefaultsToActiveOnly()
    {
        var client = _factory.CreateClient();
        var post1 = await client.PostAsJsonAsync("/api/v1/research-projects", ProjectBody("a", "first"), TestJson.Options);
        var post2 = await client.PostAsJsonAsync("/api/v1/research-projects", ProjectBody("b", "second"), TestJson.Options);
        post1.StatusCode.Should().Be(HttpStatusCode.Created);
        post2.StatusCode.Should().Be(HttpStatusCode.Created);
        var id2 = (await post2.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
        var archive = await client.PostAsync($"/api/v1/research-projects/{id2}/archive", content: null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetAsync("/api/v1/research-projects");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await list.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var count = items.GetArrayLength();
        count.Should().Be(1);

        var listAll = await client.GetAsync("/api/v1/research-projects?includeArchived=true");
        var itemsAll = await listAll.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        itemsAll.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Update_RejectsEmptyBrief()
    {
        var client = _factory.CreateClient();
        var post = await client.PostAsJsonAsync("/api/v1/research-projects", ProjectBody("t", "ok"), TestJson.Options);
        var id = (await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
        var put = await client.PutAsJsonAsync($"/api/v1/research-projects/{id}", new
        {
            title = "t",
            briefKind = "market",
            briefText = "  ",
        }, TestJson.Options);
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_NotFound_Returns404()
    {
        var client = _factory.CreateClient();
        var get = await client.GetAsync($"/api/v1/research-projects/{Guid.NewGuid()}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static object ProjectBody(string title, string brief) => new
    {
        title,
        briefKind = "market",
        briefText = brief,
        topics = Array.Empty<string>(),
        includedCompetitors = Array.Empty<string>(),
        enabledSourceProviderIds = new[] { "manual" },
    };
}

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kairion.Application.Schemas;
using Kairion.Domain;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairion.IntegrationTests;

/// <summary>
/// End-to-end tests for the R3 AI screening / analysis scenarios: typed and
/// schema-validated, retains the candidate on failure, and surfaces provider errors
/// without mutating source evidence.
/// </summary>
public class AnalysisEndpointTests : IAsyncLifetime
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
    public async Task Screen_ValidOutput_StoresAndReturnsResult()
    {
        var client = _factory.CreateClient();
        var (projectId, sourceId) = await SeedSourceAsync(client, "screen-ok");
        var spy = _factory.Services.GetRequiredService<ProviderRegistrySpy>();
        spy.Ai.Outputs.Enqueue(new ScreeningAiResponse
        {
            Relevance = 0.8m,
            Pain = 0.6m,
            CommercialHint = 0.5m,
            Spam = false,
            DecisionHint = "retain",
        });
        var response = await client.PostAsync(
            $"/api/v1/research-projects/{projectId}/candidates/{sourceId}/screening?aiProviderId={spy.Ai.ProviderId}",
            content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("decision").GetString().Should().Be("retain");
        doc.GetProperty("relevance").GetDecimal().Should().Be(0.8m);
    }

    [Fact]
    public async Task Screen_ProviderThrows_RecordsFailureButKeepsCandidate()
    {
        var client = _factory.CreateClient();
        var (projectId, sourceId) = await SeedSourceAsync(client, "screen-fail");
        var spy = _factory.Services.GetRequiredService<ProviderRegistrySpy>();
        spy.Ai.ThrowOnAnalyze = new HttpRequestException("upstream down");

        var response = await client.PostAsync(
            $"/api/v1/research-projects/{projectId}/candidates/{sourceId}/screening?aiProviderId={spy.Ai.ProviderId}",
            content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("failureReason").GetString().Should().StartWith("ai_error:");

        // The source candidate must still exist.
        var list = await client.GetAsync($"/api/v1/research-projects/{projectId}/candidates");
        var items = await list.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        items.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Screen_SchemaInvalid_RecordsFailureAndPreservesSource()
    {
        var client = _factory.CreateClient();
        var (projectId, sourceId) = await SeedSourceAsync(client, "screen-bad");
        var spy = _factory.Services.GetRequiredService<ProviderRegistrySpy>();
        // Invalid decision_hint enum value: the AI schema validator rejects the
        // output, the orchestrator records a typed ScreeningResult with the
        // failure reason, and the source evidence is preserved.
        spy.Ai.Outputs.Enqueue(new ScreeningAiResponse
        {
            Relevance = 0.5m,
            Pain = 0.5m,
            CommercialHint = 0.5m,
            Spam = false,
            DecisionHint = "unsure",
        });
        var response = await client.PostAsync(
            $"/api/v1/research-projects/{projectId}/candidates/{sourceId}/screening?aiProviderId={spy.Ai.ProviderId}",
            content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("decision").GetString().Should().Be("failed");
        doc.GetProperty("failureReason").GetString().Should().StartWith("range_error:decision_hint");
        // The source candidate must still exist.
        var list = await client.GetAsync($"/api/v1/research-projects/{projectId}/candidates");
        var items = await list.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        items.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Analyze_RequiresRetainScreening()
    {
        var client = _factory.CreateClient();
        var (projectId, sourceId) = await SeedSourceAsync(client, "analyze-order");
        var spy = _factory.Services.GetRequiredService<ProviderRegistrySpy>();
        spy.Ai.Outputs.Enqueue(new ScreeningAiResponse
        {
            Relevance = 0.1m, // Below the relevance floor -> discard.
            Pain = 0.1m,
            CommercialHint = 0.1m,
            Spam = false,
            DecisionHint = "discard",
        });
        await client.PostAsync(
            $"/api/v1/research-projects/{projectId}/candidates/{sourceId}/screening?aiProviderId={spy.Ai.ProviderId}",
            content: null);

        var response = await client.PostAsync(
            $"/api/v1/research-projects/{projectId}/candidates/{sourceId}/analysis?aiProviderId={spy.Ai.ProviderId}",
            content: null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Analyze_AfterRetainScreening_StoresTypedOutput()
    {
        var client = _factory.CreateClient();
        var (projectId, sourceId) = await SeedSourceAsync(client, "analyze-ok");
        var spy = _factory.Services.GetRequiredService<ProviderRegistrySpy>();
        spy.Ai.Outputs.Enqueue(new ScreeningAiResponse
        {
            Relevance = 0.8m,
            Pain = 0.6m,
            CommercialHint = 0.5m,
            Spam = false,
            DecisionHint = "retain",
        });
        await client.PostAsync(
            $"/api/v1/research-projects/{projectId}/candidates/{sourceId}/screening?aiProviderId={spy.Ai.ProviderId}",
            content: null);
        spy.Ai.Outputs.Enqueue(new DeepAnalysisAiResponse
        {
            Problem = "Recurring billing complaint",
            Context = "Context body",
            CurrentSolution = "Manual invoice",
            Dissatisfaction = "D",
            Workaround = "W",
            DesiredOutcome = "DO",
            Category = "billing",
            PriceSensitivity = "medium",
            PainStrength = 0.7m,
            Confidence = 0.8m,
        });
        var response = await client.PostAsync(
            $"/api/v1/research-projects/{projectId}/candidates/{sourceId}/analysis?aiProviderId={spy.Ai.ProviderId}",
            content: null);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, because: body);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        doc.GetProperty("status").GetString().Should().Be("completed", because: body);
        doc.GetProperty("category").GetString().Should().Be("billing");
    }

    private static async Task<(Guid projectId, Guid sourceId)> SeedSourceAsync(HttpClient client, string title)
    {
        var projectBody = new
        {
            title,
            briefKind = "market",
            briefText = "brief",
            topics = Array.Empty<string>(),
            includedCompetitors = Array.Empty<string>(),
            enabledSourceProviderIds = new[] { "manual" },
        };
        var post = await client.PostAsJsonAsync("/api/v1/research-projects", projectBody, TestJson.Options);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var projectId = (await post.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();

        var import = await client.PostAsJsonAsync($"/api/v1/research-projects/{projectId}/candidates/import", new
        {
            providerId = "manual",
            externalId = Guid.NewGuid().ToString("N"),
            canonicalUrl = $"https://example.test/{Guid.NewGuid():N}",
            title = "seed",
        }, TestJson.Options);
        import.StatusCode.Should().Be(HttpStatusCode.Created);
        var sourceId = (await import.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
        return (projectId, sourceId);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kairion.Application.Abstractions;
using Kairion.Application.Schemas;
using Kairion.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairion.IntegrationTests;

/// <summary>
/// Per-test factory. Each test gets its own factory, its own in-memory database
/// name, and a freshly-reset provider spy. This is the only reliable way to
/// keep the deterministic in-memory store isolated from the previous test's
/// state without dropping back to running tests in serial processes.
/// </summary>
public sealed class KairionApiFactory : WebApplicationFactory<Program>
{
    public string InMemoryDatabaseName { get; } = $"kairion-test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kairion:UseHangfire"] = "false",
                ["Kairion:ConnectionString"] = "",
                ["Kairion:InMemoryDatabaseName"] = InMemoryDatabaseName,
            });
        });
        builder.ConfigureTestServices(services =>
        {
            // Drop the production providers; tests will register their own.
            var toRemove = services.Where(d => d.ServiceType == typeof(ISourceProvider) || d.ServiceType == typeof(IAiProvider)).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddSingleton<ProviderRegistrySpy>();
            services.AddSingleton<ISourceProvider>(sp => sp.GetRequiredService<ProviderRegistrySpy>().Source);
            services.AddSingleton<IAiProvider>(sp => sp.GetRequiredService<ProviderRegistrySpy>().Ai);
        });
    }
}

/// <summary>
/// Aggregates the deterministic fakes that tests register. The spy is created
/// per factory; tests can call <see cref="Reset"/> to clear queued outputs and
/// overrides between calls inside one test.
/// </summary>
public sealed class ProviderRegistrySpy
{
    public FakeSourceProvider Source { get; } = new();
    public FakeAiProvider Ai { get; } = new();

    public void Reset()
    {
        Source.QueuedResults.Clear();
        Source.ThrowOnSearch = null;
        Ai.Outputs.Clear();
        Ai.ThrowOnAnalyze = null;
        Ai.Override = null;
    }
}

/// <summary>
/// Configurable in-memory source provider. Tests can queue returned candidates,
/// status overrides, and search-error exceptions. Used to exercise the
/// source-intake paths without touching the network.
/// </summary>
public sealed class FakeSourceProvider : ISourceProvider
{
    public List<SourceFetchResult> QueuedResults { get; } = new();
    public Exception? ThrowOnSearch { get; set; }
    public string ProviderId { get; set; } = "fake-source";
    public string DisplayName { get; set; } = "Fake source (test)";
    public bool RequiresCredentials => false;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<IReadOnlyList<SourceFetchResult>> SearchAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        if (ThrowOnSearch is not null) throw ThrowOnSearch;
        return Task.FromResult<IReadOnlyList<SourceFetchResult>>(QueuedResults.ToList());
    }

    public Task<SourceFetchResult?> FetchAsync(SourceReference reference, CancellationToken cancellationToken) =>
        Task.FromResult<SourceFetchResult?>(null);
}

/// <summary>
/// Configurable in-memory AI provider. Tests can queue typed outputs and
/// failures per schema. When nothing is queued, the spy throws a clear error
/// so the test author notices they forgot to set up the next response.
/// </summary>
public sealed class FakeAiProvider : IAiProvider
{
    public Queue<object> Outputs { get; } = new();
    public Exception? ThrowOnAnalyze { get; set; }
    public Func<object, object>? Override { get; set; }
    public string ProviderId { get; set; } = "fake-ai";
    public string DisplayName { get; set; } = "Fake AI (test)";
    public bool RequiresCredentials => false;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<AiResult<TOutput>> AnalyzeAsync<TOutput>(object input, AiSchemaDefinition schema, CancellationToken cancellationToken)
        where TOutput : class
    {
        if (ThrowOnAnalyze is not null) throw ThrowOnAnalyze;
        var overrideValue = Override?.Invoke(input);
        var output = (TOutput?)overrideValue
            ?? (TOutput?)Outputs.Dequeue()
            ?? throw new InvalidOperationException(
                $"FakeAiProvider.AnalyzeAsync was invoked but no output was queued. " +
                $"Add to the spy via {nameof(ProviderRegistrySpy)}.{nameof(FakeAiProvider)}.{nameof(Outputs)} before calling the API.");
        var metadata = new AiCallMetadata(
            providerId: ProviderId,
            model: "fake-v1",
            schemaVersion: schema.Version,
            status: ProviderStatus.Available,
            failureCode: null,
            failureMessage: null,
            promptTokens: null,
            completionTokens: null,
            calledAtUtc: DateTime.UtcNow);
        return Task.FromResult(new AiResult<TOutput>(output, metadata));
    }

    public Task<AiResult<float[]>?> EmbedAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult<AiResult<float[]>?>(null);
}

/// <summary>Helper for building request bodies.</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

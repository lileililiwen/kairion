using Kairion.Application.Abstractions;
using Kairion.Application.Schemas;
using Kairion.Application.Trends;
using Kairion.Application.UseCases;
using Kairion.Infrastructure.Jobs;
using Kairion.Infrastructure.Persistence;
using Kairion.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Hangfire;

namespace Kairion.Infrastructure;

public sealed class KairionOptions
{
    public bool UseHangfire { get; set; }
    public string? ConnectionString { get; set; }
    public string? InMemoryDatabaseName { get; set; }
}

public static class KairionServiceCollectionExtensions
{
    public static IServiceCollection AddKairionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KairionOptions>? configure = null)
    {
        // Bind options from the supplied configuration so the production path
        // (appsettings.json + env vars) keeps working unchanged.
        services.AddOptions<KairionOptions>().Bind(configuration.GetSection("Kairion"));
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddDbContext<KairionDbContext>((sp, opt) =>
        {
            // Re-resolve the configuration at DbContext construction time so test
            // harnesses that mutate configuration (WebApplicationFactory +
            // ConfigureAppConfiguration) and override callbacks (the
            // Kairion:InMemoryDatabaseName test option) take effect. We do not
            // capture the options at registration time because some hosts build
            // the configuration after the initial AddKairionInfrastructure call.
            var runtimeConfig = sp.GetService<IConfiguration>()
                ?? configuration;
            var runtimeOptions = new KairionOptions();
            runtimeConfig.GetSection("Kairion").Bind(runtimeOptions);
            if (configure is not null)
            {
                configure(runtimeOptions);
            }

            if (!string.IsNullOrWhiteSpace(runtimeOptions.ConnectionString))
            {
                opt.UseNpgsql(runtimeOptions.ConnectionString, npg => npg.MigrationsAssembly(typeof(KairionServiceCollectionExtensions).Assembly.GetName().Name));
            }
            else
            {
                // When no connection string is configured (tests, demos), use the
                // in-memory provider so the application can still be exercised. The
                // database name is read from the Kairion:InMemoryDatabaseName option
                // if set, otherwise a process-wide GUID so concurrent test runs do
                // not share state. The factory-level configuration in
                // KairionApiFactory sets a stable per-fixture name so all contexts
                // resolved by one WebApplicationFactory share the same store.
                var dbName = runtimeOptions.InMemoryDatabaseName
                    ?? $"kairion-{Guid.NewGuid():N}";
                opt.UseInMemoryDatabase(dbName);
            }
        });
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IObservationReadService, EfObservationReadService>();
        services.AddScoped<IResearchProjectRepository, ResearchProjectRepository>();
        services.AddScoped<ISourceItemRepository, SourceItemRepository>();
        services.AddScoped<IScreeningResultRepository, ScreeningResultRepository>();
        services.AddScoped<IDeepAnalysisRepository, DeepAnalysisRepository>();
        services.AddScoped<IPainClusterRepository, PainClusterRepository>();
        services.AddScoped<IClusterAssignmentRepository, ClusterAssignmentRepository>();
        services.AddScoped<IHumanRevisionRepository, HumanRevisionRepository>();
        services.AddScoped<IObservationRepository, ObservationRepository>();
        services.AddScoped<ISourceIngestionRunRepository, SourceIngestionRunRepository>();

        // Application services.
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<AiSchemaValidator>();
        services.AddSingleton(ScreeningDecisionPolicy.Default);
        services.AddSingleton<TrendThresholds>(TrendThresholds.Default);
        services.AddScoped<TrendService>();
        services.AddScoped<ResearchProjectService>();
        services.AddScoped<CandidateIntakeService>();
        services.AddScoped<AnalysisOrchestrator>();
        services.AddScoped<ClusteringService>();
        services.AddScoped<OpportunitySignalService>();

        // Source / AI providers.
        services.AddSingleton<ISourceProvider, ManualSourceProvider>();
        services.AddSingleton<ISourceProvider, DemoSearchSourceProvider>();
        services.AddHttpClient<HackerNewsSourceProvider>();
        services.AddSingleton<ISourceProvider>(sp => sp.GetRequiredService<HackerNewsSourceProvider>());
        services.AddHttpClient<ConfiguredWebSearchProvider>();
        services.AddSingleton<ISourceProvider>(sp => sp.GetRequiredService<ConfiguredWebSearchProvider>());
        services.Configure<HackerNewsOptions>(configuration.GetSection("Kairion:SourceProviders:HackerNews"));
        services.AddSingleton<IAiProvider, DeterministicDemoAiProvider>();
        services.AddSingleton<IProviderRegistry, ProviderRegistry>();

        // Jobs.
        if (configuration.GetValue<bool?>("Kairion:UseHangfire") ?? false)
        {
            // Hangfire is registered by the API project; here we only register the
            // scheduler implementation that talks to Hangfire's IBackgroundJobClient.
            services.AddScoped<IJobScheduler>(sp => new HangfireJobScheduler(
                sp.GetRequiredService<IBackgroundJobClient>(),
                sp.GetRequiredService<JobStorage>(),
                sp.GetRequiredService<ILogger<HangfireJobScheduler>>()));
        }
        else
        {
            services.AddSingleton<IJobScheduler, InProcessJobScheduler>();
        }

        return services;
    }
}

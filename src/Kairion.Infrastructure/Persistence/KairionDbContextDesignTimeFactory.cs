using Kairion.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Kairion.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to scaffold migrations when the
/// host's configuration is not available. It reads the connection string from
/// the <c>KAIRION_DESIGNTIME_CONNECTION</c> environment variable (or
/// <c>ConnectionStrings:Kairion</c> from any local appsettings) and falls back
/// to a local Postgres instance so the migration can be authored without the
/// full host wiring. The runtime path uses <c>AddKairionInfrastructure</c> and
/// this factory is never used at runtime.
/// </summary>
public sealed class KairionDbContextDesignTimeFactory : IDesignTimeDbContextFactory<KairionDbContext>
{
    public KairionDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Local default so `dotnet ef migrations add` works without
                // an explicit environment variable on developer machines.
                ["ConnectionStrings:Kairion"] = Environment.GetEnvironmentVariable("KAIRION_DESIGNTIME_CONNECTION")
                    ?? "Host=127.0.0.1;Port=5432;Database=kairion;Username=kairion;Password=kairion",
            })
            .Build();

        var connectionString = config.GetConnectionString("Kairion")
            ?? throw new InvalidOperationException(
                "Design-time factory requires ConnectionStrings:Kairion. " +
                "Set it in appsettings, the KAIRION_DESIGNTIME_CONNECTION env var, or --connection.");

        var options = new DbContextOptionsBuilder<KairionDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new KairionDbContext(options);
    }
}

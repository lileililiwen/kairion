using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using Kairion.Api.Errors;
using Kairion.Infrastructure;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging (Serilog, structured, console sink for self-hosting) ---------------
builder.Host.UseSerilog((ctx, services, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);
});

// ---- JSON ----------------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        // Use the same serializer settings the DomainExceptionHandler uses so
        // every response (DTOs and application/problem+json errors) has the
        // same camelCase, no-null-skipping, ProblemDetails-extension-bearing
        // wire format.
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    });

// ---- ProblemDetails + global exception mapping --------------------------------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// ---- Kairion services (DB, providers, jobs, use cases) -------------------------
builder.Services.AddKairionInfrastructure(builder.Configuration);

// ---- OpenAPI / Swagger ---------------------------------------------------------
// The MVP exposes the OpenAPI document at /swagger/v1/swagger.json and the
// Swashbuckle UI at /swagger for parity with the rest of the .NET ecosystem.
// Real auth (e.g. owner token) is deferred to a follow-up change that introduces
// a credential boundary.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- CORS for the local React dev server ---------------------------------------
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Hangfire wiring is opt-in via Kairion:UseHangfire=true.
if (builder.Configuration.GetValue<bool>("Kairion:UseHangfire"))
{
    var connectionString = builder.Configuration.GetConnectionString("Kairion")
        ?? throw new InvalidOperationException("Kairion:UseHangfire=true requires ConnectionStrings:Kairion to be set.");
    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(connectionString));
    builder.Services.AddHangfireServer();
}

var app = builder.Build();

// ---- Pipeline -------------------------------------------------------------------
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseRouting();

app.MapControllers();
app.MapGet("/", () => Results.Json(new
{
    name = "Kairion API",
    version = "v1",
    docs = "/swagger/v1/swagger.json",
    swagger = "/swagger",
    health = "/health",
}));
app.MapGet("/health", () => Results.Json(new
{
    status = "ok",
    utc = DateTime.UtcNow,
}));

app.Run();

// Required so the WebApplicationFactory<Program> in integration tests can pick up the entry point.
public partial class Program { }

using Kairion.Domain;

namespace Kairion.Application.Abstractions;

/// <summary>
/// Metadata returned alongside every AI provider call so that persisted records always
/// carry the model, schema, and status that produced them.
/// </summary>
public sealed class AiCallMetadata
{
    public AiCallMetadata(
        string providerId,
        string model,
        string schemaVersion,
        ProviderStatus status,
        string? failureCode,
        string? failureMessage,
        int? promptTokens,
        int? completionTokens,
        DateTime calledAtUtc)
    {
        ProviderId = providerId;
        Model = model;
        SchemaVersion = schemaVersion;
        Status = status;
        FailureCode = failureCode;
        FailureMessage = string.IsNullOrWhiteSpace(failureMessage) ? null : failureMessage.Trim();
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        CalledAtUtc = calledAtUtc.Kind == DateTimeKind.Utc
            ? calledAtUtc
            : DateTime.SpecifyKind(calledAtUtc, DateTimeKind.Utc);
    }

    public string ProviderId { get; }
    public string Model { get; }
    public string SchemaVersion { get; }
    public ProviderStatus Status { get; }
    public string? FailureCode { get; }
    public string? FailureMessage { get; }
    public int? PromptTokens { get; }
    public int? CompletionTokens { get; }
    public DateTime CalledAtUtc { get; }
}

/// <summary>
/// Outcome of an AI provider call. The typed output is non-null when <see cref="Metadata"/>'s
/// <see cref="ProviderStatus"/> is <see cref="ProviderStatus.Available"/>; otherwise the
/// failure details travel in the metadata and the output is null.
/// </summary>
public sealed class AiResult<TOutput>
{
    public AiResult(TOutput? output, AiCallMetadata metadata)
    {
        Output = output;
        Metadata = metadata;
    }

    public TOutput? Output { get; }
    public AiCallMetadata Metadata { get; }
}

/// <summary>
/// Provider-neutral AI provider contract. Implementations are responsible for serializing
/// the input, calling the vendor, validating the response against the supplied JSON
/// schema, and surfacing typed output plus metadata. The application layer persists the
/// result only after schema validation has succeeded.
/// </summary>
public interface IAiProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    bool RequiresCredentials { get; }
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Asks the provider to analyze the supplied input and produce a typed output that
    /// matches the supplied JSON schema. The provider MUST validate the response against
    /// the schema before returning; failures surface as <see cref="ProviderStatus.InvalidResponse"/>
    /// or the matching classified status.
    /// </summary>
    Task<AiResult<TOutput>> AnalyzeAsync<TOutput>(
        object input,
        AiSchemaDefinition schema,
        CancellationToken cancellationToken)
        where TOutput : class;

    /// <summary>
    /// Asks the provider to embed the input text into a vector. Optional; the MVP does
    /// not require embeddings, but the seam is reserved for the first clustering iteration
    /// that does.
    /// </summary>
    Task<AiResult<float[]>?> EmbedAsync(string text, CancellationToken cancellationToken);
}

/// <summary>
/// A versioned JSON schema definition. The application passes the exact schema; the
/// provider is forbidden from relaxing it. Schemas are stored alongside the typed
/// analysis rows so re-validation is possible after a provider upgrade.
/// </summary>
public sealed class AiSchemaDefinition
{
    public AiSchemaDefinition(string name, string version, string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Schema name is required.");
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("Schema version is required.");
        if (string.IsNullOrWhiteSpace(schemaJson)) throw new ArgumentException("Schema JSON is required.");
        Name = name.Trim();
        Version = version.Trim();
        SchemaJson = schemaJson;
    }

    public string Name { get; }
    public string Version { get; }
    public string SchemaJson { get; }
    public string Identity => $"{Name}@{Version}";
}

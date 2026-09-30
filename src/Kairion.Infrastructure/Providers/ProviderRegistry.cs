using Kairion.Application.Abstractions;

namespace Kairion.Infrastructure.Providers;

/// <summary>
/// In-memory provider registry. The MVP exposes the manual / demo source and the
/// deterministic demo AI provider. Additional adapters register themselves here.
/// </summary>
public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly Dictionary<string, ISourceProvider> _sourceProviders;
    private readonly Dictionary<string, IAiProvider> _aiProviders;

    public ProviderRegistry(IEnumerable<ISourceProvider> sourceProviders, IEnumerable<IAiProvider> aiProviders)
    {
        _sourceProviders = sourceProviders.ToDictionary(p => p.ProviderId, StringComparer.OrdinalIgnoreCase);
        _aiProviders = aiProviders.ToDictionary(p => p.ProviderId, StringComparer.OrdinalIgnoreCase);
    }

    public ISourceProvider? FindSourceProvider(string providerId) =>
        _sourceProviders.TryGetValue(providerId ?? string.Empty, out var p) ? p : null;

    public IAiProvider? FindAiProvider(string providerId) =>
        _aiProviders.TryGetValue(providerId ?? string.Empty, out var p) ? p : null;

    public IReadOnlyList<ISourceProvider> ListSourceProviders() => _sourceProviders.Values.ToList();

    public IReadOnlyList<IAiProvider> ListAiProviders() => _aiProviders.Values.ToList();
}

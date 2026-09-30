using Kairion.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Kairion.Api.Controllers;

/// <summary>
/// Read-only endpoints that expose the configured source and AI providers. Secrets are
/// never returned; only metadata needed to drive the web app's provider pickers.
/// </summary>
[ApiController]
[Route("api/v1/providers")]
public sealed class ProvidersController : ControllerBase
{
    private readonly IProviderRegistry _registry;

    public ProvidersController(IProviderRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet("source")]
    [ProducesResponseType(typeof(IReadOnlyList<SourceProviderHealthInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSourceProviders(CancellationToken cancellationToken)
    {
        var list = new List<SourceProviderHealthInfo>();
        foreach (var p in _registry.ListSourceProviders())
        {
            bool available;
            try
            {
                available = await p.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                available = false;
            }
            list.Add(new SourceProviderHealthInfo(p.ProviderId, p.DisplayName, p.RequiresCredentials, available));
        }
        return Ok(list);
    }

    [HttpGet("ai")]
    [ProducesResponseType(typeof(IReadOnlyList<AiProviderInfo>), StatusCodes.Status200OK)]
    public IActionResult ListAiProviders()
    {
        var providers = _registry.ListAiProviders()
            .Select(p => new AiProviderInfo(p.ProviderId, p.DisplayName, p.RequiresCredentials))
            .ToList();
        return Ok(providers);
    }
}

public sealed record SourceProviderInfo(string ProviderId, string DisplayName, bool RequiresCredentials);
public sealed record SourceProviderHealthInfo(string ProviderId, string DisplayName, bool RequiresCredentials, bool Available);
public sealed record AiProviderInfo(string ProviderId, string DisplayName, bool RequiresCredentials);

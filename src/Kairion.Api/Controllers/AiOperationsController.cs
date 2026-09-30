using Kairion.Application.Dtos;
using Kairion.Application.UseCases;
using Kairion.Api.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Kairion.Api.Controllers;

/// <summary>
/// Convenience aliases for the staged AI endpoints. The primary surface is nested under
/// <c>/api/v1/research-projects/{id}/candidates/{sourceItemId}/...</c> but these routes
/// let the web app drive the orchestrator without carrying the project id when it has
/// already loaded a source item.
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class AiOperationsController : ControllerBase
{
    private readonly AnalysisOrchestrator _analysis;

    public AiOperationsController(AnalysisOrchestrator analysis)
    {
        _analysis = analysis;
    }

    [HttpPost("ai-screening")]
    [ProducesResponseType(typeof(ScreeningOutcomeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> ScreenAsync([FromBody] AiScreeningRequest request, CancellationToken cancellationToken)
    {
        return ScreenInternalAsync(request, cancellationToken);
    }

    [HttpPost("ai-analysis")]
    [ProducesResponseType(typeof(DeepAnalysisDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> AnalyzeAsync([FromBody] AiAnalysisRequest request, CancellationToken cancellationToken)
    {
        return AnalyzeInternalAsync(request, cancellationToken);
    }

    private async Task<IActionResult> ScreenInternalAsync(AiScreeningRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceItemId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "sourceItemId is required.",
                Type = "https://kairion.dev/errors/validation_failed",
            });
        }
        if (string.IsNullOrWhiteSpace(request.AiProviderId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "aiProviderId is required.",
                Type = "https://kairion.dev/errors/validation_failed",
            });
        }
        var result = await _analysis.ScreenAsync(request.SourceItemId, request.AiProviderId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    private async Task<IActionResult> AnalyzeInternalAsync(AiAnalysisRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceItemId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "sourceItemId is required.",
                Type = "https://kairion.dev/errors/validation_failed",
            });
        }
        if (string.IsNullOrWhiteSpace(request.AiProviderId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "aiProviderId is required.",
                Type = "https://kairion.dev/errors/validation_failed",
            });
        }
        var result = await _analysis.AnalyzeAsync(request.SourceItemId, request.AiProviderId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }
}

public sealed class AiScreeningRequest
{
    public Guid SourceItemId { get; set; }
    public string AiProviderId { get; set; } = "deterministic-demo";
}

public sealed class AiAnalysisRequest
{
    public Guid SourceItemId { get; set; }
    public string AiProviderId { get; set; } = "deterministic-demo";
}

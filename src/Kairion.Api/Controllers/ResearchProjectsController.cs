using Kairion.Application.Dtos;
using Kairion.Application.Trends;
using Kairion.Application.UseCases;
using Kairion.Api.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Kairion.Api.Controllers;

/// <summary>
/// CRUD endpoints for research projects plus the nested resource endpoints for
/// candidates, clusters, and trends that all live under the project aggregate root.
/// </summary>
[ApiController]
[Route("api/v1/research-projects")]
public sealed class ResearchProjectsController : ControllerBase
{
    private readonly ResearchProjectService _projects;
    private readonly CandidateIntakeService _intake;
    private readonly AnalysisOrchestrator _analysis;
    private readonly ClusteringService _clusters;
    private readonly OpportunitySignalService _opportunity;
    private readonly ILogger<ResearchProjectsController> _logger;

    public ResearchProjectsController(
        ResearchProjectService projects,
        CandidateIntakeService intake,
        AnalysisOrchestrator analysis,
        ClusteringService clusters,
        OpportunitySignalService opportunity,
        ILogger<ResearchProjectsController> logger)
    {
        _projects = projects;
        _intake = intake;
        _analysis = analysis;
        _clusters = clusters;
        _opportunity = opportunity;
        _logger = logger;
    }

    // ---- Project CRUD --------------------------------------------------------

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ResearchProjectResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var items = await _projects.ListAsync(includeArchived, cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    [HttpGet("{id:guid}", Name = "GetResearchProject")]
    [ProducesResponseType(typeof(ResearchProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projects.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ResearchProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateResearchProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _projects.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ResearchProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateResearchProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _projects.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(typeof(ResearchProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projects.ArchiveAsync(id, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(typeof(ResearchProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projects.RestoreAsync(id, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return Ok(result.Value);
    }

    // ---- Candidates / intake -------------------------------------------------

    [HttpGet("{id:guid}/candidates")]
    [ProducesResponseType(typeof(IReadOnlyList<SourceItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCandidatesAsync(Guid id, CancellationToken cancellationToken)
    {
        var items = await _intake.ListForProjectAsync(id, cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    [HttpPost("{id:guid}/candidates/import")]
    [ProducesResponseType(typeof(SourceItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportCandidateAsync(Guid id, [FromBody] ImportCandidateRequest request, CancellationToken cancellationToken)
    {
        var result = await _intake.ImportManualAsync(id, request, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPost("{id:guid}/candidates/search")]
    [ProducesResponseType(typeof(IReadOnlyList<SourceItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SearchCandidatesAsync(
        Guid id,
        [FromQuery] string providerId,
        [FromBody] RunSourceQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "providerId query parameter is required.",
                Type = "https://kairion.dev/errors/validation_failed",
            });
        }
        var result = await _intake.RunSourceQueryAsync(id, providerId, request, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return Ok(result.Value);
    }

    // ---- AI screening / analysis --------------------------------------------

    [HttpPost("{id:guid}/candidates/{sourceItemId:guid}/screening")]
    [ProducesResponseType(typeof(ScreeningOutcomeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScreenCandidateAsync(
        Guid id,
        Guid sourceItemId,
        [FromQuery] string aiProviderId,
        CancellationToken cancellationToken)
    {
        _ = id; // route is hierarchical; the use case resolves the source item directly.
        if (string.IsNullOrWhiteSpace(aiProviderId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "aiProviderId query parameter is required.",
                Type = "https://kairion.dev/errors/validation_failed",
            });
        }
        var result = await _analysis.ScreenAsync(sourceItemId, aiProviderId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("{id:guid}/candidates/{sourceItemId:guid}/screening")]
    [ProducesResponseType(typeof(ScreeningOutcomeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LatestScreeningAsync(Guid id, Guid sourceItemId, CancellationToken cancellationToken)
    {
        _ = id;
        var screening = await _analysis.LatestScreeningAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        return screening is null ? NotFound() : Ok(screening);
    }

    [HttpPost("{id:guid}/candidates/{sourceItemId:guid}/analysis")]
    [ProducesResponseType(typeof(DeepAnalysisDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AnalyzeCandidateAsync(
        Guid id,
        Guid sourceItemId,
        [FromQuery] string aiProviderId,
        CancellationToken cancellationToken)
    {
        _ = id;
        if (string.IsNullOrWhiteSpace(aiProviderId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "aiProviderId query parameter is required.",
                Type = "https://kairion.dev/errors/validation_failed",
            });
        }
        var result = await _analysis.AnalyzeAsync(sourceItemId, aiProviderId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("{id:guid}/candidates/{sourceItemId:guid}/analysis")]
    [ProducesResponseType(typeof(DeepAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LatestAnalysisAsync(Guid id, Guid sourceItemId, CancellationToken cancellationToken)
    {
        _ = id;
        var analysis = await _analysis.LatestAnalysisAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        return analysis is null ? NotFound() : Ok(analysis);
    }

    // ---- Clusters ------------------------------------------------------------

    [HttpGet("{id:guid}/clusters")]
    [ProducesResponseType(typeof(IReadOnlyList<ClusterResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListClustersAsync(Guid id, CancellationToken cancellationToken)
    {
        var clusters = await _clusters.ListForProjectAsync(id, cancellationToken).ConfigureAwait(false);
        return Ok(clusters);
    }

    [HttpPost("{id:guid}/clusters")]
    [ProducesResponseType(typeof(ClusterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateClusterAsync(
        Guid id,
        [FromBody] CreateClusterApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _clusters.EnsureClusterAsync(id, request.Label, request.Category, request.Summary, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    // ---- Trends --------------------------------------------------------------

    [HttpGet("{id:guid}/trends")]
    [ProducesResponseType(typeof(TrendResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProjectTrendsAsync(
        Guid id,
        [FromQuery] string window = "30d",
        CancellationToken cancellationToken = default)
    {
        if (!Kairion.Application.Trends.TrendWindowExtensions.TryParse(window, out var trendWindow))
        {
            return this.BadRequest(ProblemDetailsResults.Build(
                controller: this,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed",
                message: $"Unsupported trend window '{window}'. Use one of: 7d, 30d, 90d.",
                code: "validation_failed"));
        }
        // The trend service is scoped on the controller's lifetime; we route through it via
        // the same DI scope the request creates.
        var service = HttpContext.RequestServices.GetRequiredService<TrendService>();
        var response = await service.ComputeProjectTrendAsync(id, trendWindow, cancellationToken).ConfigureAwait(false);
        return Ok(response);
    }

    // ---- Opportunity signals -------------------------------------------------

    [HttpGet("{id:guid}/opportunity-signals/{clusterId:guid}")]
    [ProducesResponseType(typeof(OpportunitySignalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOpportunitySignalAsync(
        Guid id,
        Guid clusterId,
        [FromQuery] string window = "30d",
        [FromQuery] string aiProviderId = "deterministic-demo",
        CancellationToken cancellationToken = default)
    {
        _ = id;
        if (!Kairion.Application.Trends.TrendWindowExtensions.TryParse(window, out _))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = $"Unsupported trend window '{window}'. Use one of: 7d, 30d, 90d.",
                Type = "https://kairion.dev/errors/validation_failed",
            });
        }
        var signal = await _opportunity.BuildAsync(clusterId, window, aiProviderId, cancellationToken).ConfigureAwait(false);
        return Ok(signal);
    }
}

/// <summary>
/// Cluster create payload sent over the API; the use case accepts raw arguments so this
/// DTO is a thin wire format only.
/// </summary>
public sealed class CreateClusterApiRequest
{
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

using Kairion.Application.Dtos;
using Kairion.Application.UseCases;
using Kairion.Api.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Kairion.Api.Controllers;

/// <summary>
/// Cluster-level operations: evidence board, human edits, reassignments, merges and
/// splits. Every mutation that comes from a human is recorded as an auditable
/// <c>HumanRevision</c> that wins over later AI suggestions.
/// </summary>
[ApiController]
[Route("api/v1/clusters")]
public sealed class ClustersController : ControllerBase
{
    private readonly ClusteringService _clusters;
    private readonly ILogger<ClustersController> _logger;

    public ClustersController(ClusteringService clusters, ILogger<ClustersController> logger)
    {
        _clusters = clusters;
        _logger = logger;
    }

    [HttpGet("{id:guid}", Name = "GetCluster")]
    [ProducesResponseType(typeof(ClusterEvidenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvidenceAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _clusters.GetEvidenceAsync(id, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ClusterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditClusterAsync(Guid id, [FromBody] UpdateClusterRequest request, CancellationToken cancellationToken)
    {
        var result = await _clusters.EditClusterAsync(id, request, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/reassign")]
    [ProducesResponseType(typeof(ClusterAssignmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReassignAsync(Guid id, [FromBody] ReassignItemRequest request, CancellationToken cancellationToken)
    {
        // The body provides the target cluster explicitly; if it differs from the route
        // we accept it but the use case routes by target cluster. We allow the mismatch
        // so the web UI can POST to a canonical "reassign into this cluster" endpoint
        // while still sending the source's previous cluster in the body.
        var target = request.TargetClusterId == Guid.Empty ? id : request.TargetClusterId;
        var result = await _clusters.ReassignAsync(target, new ReassignItemRequest
        {
            SourceItemId = request.SourceItemId,
            TargetClusterId = target,
        }, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return Ok(ClusterAssignmentResponse.From(result.Value));
    }

    [HttpPost("{id:guid}/merge")]
    [ProducesResponseType(typeof(ClusterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MergeAsync(Guid id, [FromBody] MergeClustersRequest request, CancellationToken cancellationToken)
    {
        var target = request.TargetClusterId == Guid.Empty ? id : request.TargetClusterId;
        var result = await _clusters.MergeClustersAsync(target, new MergeClustersRequest
        {
            SourceClusterId = request.SourceClusterId,
            TargetClusterId = target,
        }, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/split")]
    [ProducesResponseType(typeof(ClusterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SplitAsync(Guid id, [FromBody] SplitClusterRequest request, CancellationToken cancellationToken)
    {
        var result = await _clusters.SplitClusterAsync(id, request, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return this.FromResult(result, successLocation: string.Empty);
        }
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("{id:guid}/revisions")]
    [ProducesResponseType(typeof(IReadOnlyList<HumanRevisionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevisionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var revisions = await _clusters.RevisionsForClusterAsync(id, cancellationToken).ConfigureAwait(false);
        return Ok(revisions);
    }
}

/// <summary>
/// Wire format for a cluster assignment row. The domain assignment is mapped 1:1 so the
/// web app can render the most recent origin (Human vs AI) without a second round-trip.
/// </summary>
public sealed class ClusterAssignmentResponse
{
    public Guid Id { get; set; }
    public Guid ClusterId { get; set; }
    public Guid SourceItemId { get; set; }
    public Guid? DeepAnalysisId { get; set; }
    public string Origin { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public Guid? SupersedesId { get; set; }

    public static ClusterAssignmentResponse From(Kairion.Domain.ClusterAssignment a) => new()
    {
        Id = a.Id,
        ClusterId = a.ClusterId,
        SourceItemId = a.SourceItemId,
        DeepAnalysisId = a.DeepAnalysisId,
        Origin = a.Origin.ToString(),
        CreatedUtc = a.CreatedUtc,
        SupersedesId = a.SupersedesId,
    };
}

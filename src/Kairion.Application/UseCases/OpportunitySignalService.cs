using Kairion.Application.Abstractions;
using Kairion.Application.Dtos;
using Kairion.Application.Schemas;
using Kairion.Application.Trends;
using Kairion.Domain;

namespace Kairion.Application.UseCases;

/// <summary>
/// Builds the Opportunity Signal read model for a cluster. The signal is a deterministic
/// projection of the cluster's persisted evidence plus an AI-assisted explanation that
/// is always labeled as informational. No aggregate value originates from the AI call.
/// </summary>
public sealed class OpportunitySignalService
{
    private readonly IPainClusterRepository _clusters;
    private readonly IClusterAssignmentRepository _assignments;
    private readonly IDeepAnalysisRepository _analyses;
    private readonly IObservationReadService _observations;
    private readonly IProviderRegistry _providers;
    private readonly AiSchemaValidator _schemaValidator;
    private readonly TrendService _trendService;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHumanRevisionRepository _revisions;

    public OpportunitySignalService(
        IPainClusterRepository clusters,
        IClusterAssignmentRepository assignments,
        IDeepAnalysisRepository analyses,
        IObservationReadService observations,
        IProviderRegistry providers,
        AiSchemaValidator schemaValidator,
        TrendService trendService,
        IClock clock,
        IUnitOfWork unitOfWork,
        IHumanRevisionRepository revisions)
    {
        _clusters = clusters;
        _assignments = assignments;
        _analyses = analyses;
        _observations = observations;
        _providers = providers;
        _schemaValidator = schemaValidator;
        _trendService = trendService;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _revisions = revisions;
    }

    public async Task<OpportunitySignalResponse> BuildAsync(
        Guid clusterId,
        string window,
        string aiProviderId,
        CancellationToken cancellationToken)
    {
        if (!TrendWindowExtensions.TryParse(window, out var trendWindow))
        {
            trendWindow = TrendWindow.Days30;
        }
        var cluster = await _clusters.FindAsync(clusterId, cancellationToken).ConfigureAwait(false);
        if (cluster is null)
        {
            return EmptySignal(clusterId, trendWindow, "Cluster not found.");
        }
        var asOf = _clock.UtcNow;
        var (windowStart, windowEnd) = _trendService.ComputeWindow(asOf, trendWindow);
        var currentIds = await _observations
            .DistinctSourceIdsInWindowAsync(cluster.ProjectId, windowStart, windowEnd, cluster.Id, cancellationToken)
            .ConfigureAwait(false);
        if (currentIds.Count == 0)
        {
            var empty = OpportunitySignal.Empty(
                id: Guid.NewGuid(),
                clusterId: cluster.Id,
                asOfUtc: asOf,
                windowStartUtc: windowStart,
                windowEndUtc: windowEnd,
                generatedUtc: asOf,
                window: trendWindow.Label(),
                reason: "No retained source items in the selected window for this cluster.");
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new OpportunitySignalResponse
            {
                Id = empty.Id,
                ClusterId = empty.ClusterId,
                EvidenceVolume = 0,
                PercentGrowth = null,
                NewSignal = false,
                CurrentAlternatives = new List<string>(),
                Workarounds = new List<string>(),
                Confidence = 0m,
                AiExplanation = empty.AiExplanation,
                TrendLabel = empty.TrendLabel,
                Window = empty.Window,
                AsOfUtc = empty.AsOfUtc,
                WindowStartUtc = empty.WindowStartUtc,
                WindowEndUtc = empty.WindowEndUtc,
                GeneratedUtc = empty.GeneratedUtc,
                RepresentativeSourceItemIds = new List<Guid>(),
            };
        }

        var (previousStart, previousEnd) = _trendService.ComputePreviousWindow(asOf, trendWindow);
        var previousIds = await _observations
            .DistinctSourceIdsInWindowAsync(cluster.ProjectId, previousStart, previousEnd, cluster.Id, cancellationToken)
            .ConfigureAwait(false);
        var (percentGrowth, newSignal) = _trendService.ComputeGrowth(currentIds.Count, previousIds.Count);
        var label = _trendService.LabelFor(percentGrowth, newSignal);

        var alternatives = new List<string>();
        var workarounds = new List<string>();
        decimal totalConfidence = 0m;
        int confidenceCount = 0;
        var representative = new List<Guid>();
        foreach (var sourceId in currentIds.Take(5))
        {
            representative.Add(sourceId);
            var analysis = await _analyses.LatestForSourceAsync(sourceId, cancellationToken).ConfigureAwait(false);
            if (analysis is { Status: AnalysisStatus.Completed })
            {
                totalConfidence += analysis.Confidence;
                confidenceCount += 1;
                if (!string.IsNullOrWhiteSpace(analysis.CurrentSolution) && alternatives.Count < 5)
                {
                    alternatives.Add(analysis.CurrentSolution);
                }
                if (!string.IsNullOrWhiteSpace(analysis.Workaround) && workarounds.Count < 5)
                {
                    workarounds.Add(analysis.Workaround);
                }
            }
        }
        var confidence = confidenceCount == 0 ? 0m : Math.Round(totalConfidence / confidenceCount, 4, MidpointRounding.ToEven);

        var provider = _providers.FindAiProvider(aiProviderId);
        string? explanation = null;
        if (provider is not null)
        {
            try
            {
                var input = new
                {
                    label = cluster.Label,
                    category = cluster.Category,
                    summary = cluster.Summary,
                    evidenceCount = currentIds.Count,
                    window = trendWindow.Label(),
                    alternatives = alternatives.ToArray(),
                    workarounds = workarounds.ToArray(),
                };
                var raw = await provider.AnalyzeAsync<ClusterExplanationAiResponse>(input, AiSchemas.ClusterExplanationV1, cancellationToken).ConfigureAwait(false);
                var json = System.Text.Json.JsonSerializer.Serialize(raw.Output, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
                var validated = _schemaValidator.ValidateAndDeserialize<ClusterExplanationAiResponse>(json, AiSchemas.ClusterExplanationV1);
                if (validated.Output is not null)
                {
                    explanation = validated.Output.Explanation;
                    if (alternatives.Count == 0) alternatives = validated.Output.Alternatives.ToList();
                    if (workarounds.Count == 0) workarounds = validated.Output.Workarounds.ToList();
                    if (confidence == 0m) confidence = validated.Output.Confidence;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Provider failure is non-fatal for the signal: we already have a
                // deterministic projection, so we simply omit the AI explanation.
                explanation = null;
            }
        }
        else
        {
            explanation = "No AI provider configured; only deterministic cluster statistics are shown.";
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new OpportunitySignalResponse
        {
            Id = Guid.NewGuid(),
            ClusterId = cluster.Id,
            EvidenceVolume = currentIds.Count,
            PercentGrowth = percentGrowth,
            NewSignal = newSignal,
            CurrentAlternatives = alternatives,
            Workarounds = workarounds,
            Confidence = confidence,
            AiExplanation = explanation,
            TrendLabel = label,
            Window = trendWindow.Label(),
            AsOfUtc = asOf,
            WindowStartUtc = windowStart,
            WindowEndUtc = windowEnd,
            GeneratedUtc = asOf,
            RepresentativeSourceItemIds = representative,
        };
    }

    private static OpportunitySignalResponse EmptySignal(Guid clusterId, TrendWindow window, string reason)
    {
        var asOf = DateTime.UtcNow;
        var (start, end) = window switch
        {
            TrendWindow.Days7 => (asOf.AddDays(-7), asOf),
            TrendWindow.Days30 => (asOf.AddDays(-30), asOf),
            TrendWindow.Days90 => (asOf.AddDays(-90), asOf),
            _ => (asOf.AddDays(-30), asOf),
        };
        return new OpportunitySignalResponse
        {
            Id = Guid.NewGuid(),
            ClusterId = clusterId,
            EvidenceVolume = 0,
            PercentGrowth = null,
            NewSignal = false,
            CurrentAlternatives = new List<string>(),
            Workarounds = new List<string>(),
            Confidence = 0m,
            AiExplanation = reason,
            TrendLabel = "no_evidence",
            Window = window.Label(),
            AsOfUtc = asOf,
            WindowStartUtc = start,
            WindowEndUtc = end,
            GeneratedUtc = asOf,
            RepresentativeSourceItemIds = new List<Guid>(),
        };
    }
}

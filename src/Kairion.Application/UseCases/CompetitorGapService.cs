using Kairion.Application.Abstractions;
using Kairion.Application.Dtos;
using Kairion.Application.Trends;
using Kairion.Domain;

namespace Kairion.Application.UseCases;

/// <summary>
/// Read-model service for the competitor-gap matrix. All counts and deltas are
/// computed deterministically from persisted timestamps and explicit
/// assignments; the service never infers a competitor from free text and never
/// calls a source or AI provider.
/// </summary>
public sealed class CompetitorGapService
{
    public const string UnmappedName = "Unmapped";
    private const int LimitedEvidenceThreshold = 3;
    private const int MaxRepresentative = 3;

    private readonly IResearchProjectRepository _projects;
    private readonly IPainClusterRepository _clusters;
    private readonly IClusterAssignmentRepository _assignments;
    private readonly ISourceItemRepository _sourceItems;
    private readonly IDeepAnalysisRepository _analyses;
    private readonly ICompetitorRepository _competitors;
    private readonly ISourceItemCompetitorRepository _itemCompetitors;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly TrendService _trends;

    public CompetitorGapService(
        IResearchProjectRepository projects,
        IPainClusterRepository clusters,
        IClusterAssignmentRepository assignments,
        ISourceItemRepository sourceItems,
        IDeepAnalysisRepository analyses,
        ICompetitorRepository competitors,
        ISourceItemCompetitorRepository itemCompetitors,
        IUnitOfWork unitOfWork,
        IClock clock,
        TrendService trends)
    {
        _projects = projects;
        _clusters = clusters;
        _assignments = assignments;
        _sourceItems = sourceItems;
        _analyses = analyses;
        _competitors = competitors;
        _itemCompetitors = itemCompetitors;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _trends = trends;
    }

    /// <summary>
    /// Ensures every name in <paramref name="names"/> has a stable competitor row.
    /// Existing rows are reused by normalized name, so IDs stay stable across edits.
    /// Rows for removed names are retained so existing evidence stays linked.
    /// </summary>
    public async Task<IReadOnlyList<Competitor>> EnsureForProjectAsync(
        Guid projectId, IEnumerable<string>? names, CancellationToken cancellationToken)
    {
        var existing = await _competitors.ListForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        var byNormalized = existing.ToDictionary(c => c.NormalizedName, c => c, StringComparer.Ordinal);
        var now = _clock.UtcNow;
        var result = new List<Competitor>(existing);
        foreach (var raw in names ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var name = raw.Trim();
            if (name.Length > 200) continue;
            var normalized = Competitor.Normalize(name);
            if (byNormalized.ContainsKey(normalized)) continue;
            var competitor = new Competitor(Guid.NewGuid(), projectId, name, now);
            await _competitors.AddAsync(competitor, cancellationToken).ConfigureAwait(false);
            byNormalized[normalized] = competitor;
            result.Add(competitor);
        }
        return result;
    }

    public async Task<IReadOnlyList<CompetitorResponse>> ListAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null || project.State == ResearchProjectState.Archived) return Array.Empty<CompetitorResponse>();
        await EnsureForProjectAsync(projectId, project.IncludedCompetitors, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var rows = await _competitors.ListForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        return rows.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(ToResponse).ToList();
    }

    public async Task<Result<CompetitorResponse>> AssignAsync(
        Guid projectId, Guid sourceItemId, Guid competitorId, AssignmentOrigin origin, CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null || project.State == ResearchProjectState.Archived)
            return Result<CompetitorResponse>.Failure($"Research project {projectId} not found.");
        var source = await _sourceItems.FindAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        if (source is null || source.ProjectId != projectId)
            return Result<CompetitorResponse>.Failure($"Source item {sourceItemId} not found in this project.");
        var competitor = await _competitors.FindAsync(competitorId, cancellationToken).ConfigureAwait(false);
        if (competitor is null || competitor.ProjectId != projectId)
            return Result<CompetitorResponse>.Failure($"Competitor {competitorId} does not belong to this project.");

        var existing = await _itemCompetitors.ListForSourceAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        if (existing.Any(a => a.CompetitorId == competitorId))
            return Result<CompetitorResponse>.Success(ToResponse(competitor));

        await _itemCompetitors.AddAsync(new SourceItemCompetitorAssignment(
            Guid.NewGuid(), projectId, sourceItemId, competitorId, origin, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<CompetitorResponse>.Success(ToResponse(competitor));
    }

    public async Task<Result<bool>> UnassignAsync(
        Guid projectId, Guid sourceItemId, Guid competitorId, CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null || project.State == ResearchProjectState.Archived)
            return Result<bool>.Failure($"Research project {projectId} not found.");
        var rows = await _itemCompetitors.ListForSourceAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        var match = rows.FirstOrDefault(a => a.CompetitorId == competitorId && a.ProjectId == projectId);
        if (match is null) return Result<bool>.Success(false);
        _itemCompetitors.Remove(match);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Computes the competitor-by-cluster matrix. Returns null when the project is
    /// missing or archived (the controller maps this to 404). Throws
    /// <see cref="DomainValidationException"/> for foreign competitor filters.
    /// </summary>
    public async Task<CompetitorGapResponse?> ComputeAsync(
        Guid projectId, TrendWindow window, IReadOnlyList<Guid>? competitorFilter, CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null || project.State == ResearchProjectState.Archived) return null;

        await EnsureForProjectAsync(projectId, project.IncludedCompetitors, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var asOf = _clock.UtcNow;
        var (windowStart, windowEnd) = _trends.ComputeWindow(asOf, window);
        var (prevStart, prevEnd) = _trends.ComputePreviousWindow(asOf, window);

        var allCompetitors = (await _competitors.ListForProjectAsync(projectId, cancellationToken).ConfigureAwait(false))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (competitorFilter is { Count: > 0 })
        {
            var known = allCompetitors.Select(c => c.Id).ToHashSet();
            var foreign = competitorFilter.Where(id => !known.Contains(id)).ToList();
            if (foreign.Count > 0)
                throw new DomainValidationException($"Competitor {foreign[0]} does not belong to this project.");
            allCompetitors = allCompetitors.Where(c => competitorFilter.Contains(c.Id)).ToList();
        }

        var clusters = (await _clusters.ListForProjectAsync(projectId, cancellationToken).ConfigureAwait(false))
            .OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase).ToList();
        var sources = await _sourceItems.ListForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);

        // Latest cluster assignment per source item (project-scoped).
        var latestCluster = new Dictionary<Guid, Guid>();
        foreach (var cluster in clusters)
        {
            var rows = await _assignments.ListForClusterAsync(cluster.Id, cancellationToken).ConfigureAwait(false);
            foreach (var group in rows.GroupBy(a => a.SourceItemId))
            {
                var latest = group.OrderByDescending(a => a.CreatedUtc).First();
                latestCluster[latest.SourceItemId] = latest.ClusterId;
            }
        }

        // Explicit competitor assignments per source item.
        var itemCompetitorIds = new Dictionary<Guid, HashSet<Guid>>();
        var allLinks = await _itemCompetitors.ListForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        foreach (var link in allLinks)
        {
            if (!itemCompetitorIds.TryGetValue(link.SourceItemId, out var set))
            {
                set = new HashSet<Guid>();
                itemCompetitorIds[link.SourceItemId] = set;
            }
            set.Add(link.CompetitorId);
        }

        var clusterById = clusters.ToDictionary(c => c.Id);
        var competitorById = allCompetitors.ToDictionary(c => c.Id);
        // Include every competitor known to the project in the response header,
        // even when a competitorId filter narrows the matrix rows.
        var headerCompetitors = (await _competitors.ListForProjectAsync(projectId, cancellationToken).ConfigureAwait(false))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();

        bool InWindow(DateTime effective, DateTime start, DateTime end) => effective >= start && effective <= end;

        var canonical = sources.Where(s => s.DuplicateOfId is null).ToList();
        var currentItems = canonical.Where(s => InWindow(s.EffectiveDateUtc, windowStart, windowEnd)).ToList();
        var previousItems = canonical.Where(s => InWindow(s.EffectiveDateUtc, prevStart, prevEnd)).ToList();

        var cells = new List<CompetitorGapCell>();
        // One row per (competitor or Unmapped) x cluster.
        var bucketKeys = new List<(Guid? competitorId, string name)>(
            allCompetitors.Select(c => ((Guid?)c.Id, c.Name)));
        bucketKeys.Add((null, UnmappedName));

        foreach (var (competitorId, competitorName) in bucketKeys)
        {
            foreach (var cluster in clusters)
            {
                var inCurrent = currentItems.Where(s =>
                    latestCluster.TryGetValue(s.Id, out var cid) && cid == cluster.Id &&
                    CompetitorMatches(s.Id, competitorId, itemCompetitorIds)).ToList();
                var inPrevious = previousItems.Where(s =>
                    latestCluster.TryGetValue(s.Id, out var cid) && cid == cluster.Id &&
                    CompetitorMatches(s.Id, competitorId, itemCompetitorIds)).ToList();
                cells.Add(await BuildCellAsync(
                    competitorId, competitorName, cluster, inCurrent, inPrevious,
                    asOf, cancellationToken).ConfigureAwait(false));
            }
        }

        var mappedIds = new HashSet<Guid>(currentItems
            .Where(s => itemCompetitorIds.TryGetValue(s.Id, out var set) && set.Count > 0)
            .Select(s => s.Id));
        var staleCount = currentItems.Count(s => IsStale(s, asOf));

        return new CompetitorGapResponse
        {
            ProjectId = projectId,
            Window = window.Label(),
            AsOfUtc = asOf,
            WindowStartUtc = windowStart,
            WindowEndUtc = windowEnd,
            PreviousWindowStartUtc = prevStart,
            PreviousWindowEndUtc = prevEnd,
            Coverage = new CompetitorGapCoverage
            {
                TotalEvidenceInWindow = currentItems.Count(s => latestCluster.ContainsKey(s.Id)),
                MappedEvidenceInWindow = currentItems.Count(s => latestCluster.ContainsKey(s.Id) && mappedIds.Contains(s.Id)),
                UnmappedEvidenceInWindow = currentItems.Count(s => latestCluster.ContainsKey(s.Id) && !mappedIds.Contains(s.Id)),
                CompetitorCount = headerCompetitors.Count,
                ClusterCount = clusters.Count,
                StaleEvidenceCount = staleCount,
            },
            Competitors = headerCompetitors.Select(ToResponse).ToList(),
            Cells = cells,
        };
    }

    private static bool CompetitorMatches(
        Guid sourceId, Guid? competitorId, Dictionary<Guid, HashSet<Guid>> links)
    {
        var hasAny = links.TryGetValue(sourceId, out var set) && set.Count > 0;
        if (competitorId is null) return !hasAny;
        return hasAny && set.Contains(competitorId.Value);
    }

    private async Task<CompetitorGapCell> BuildCellAsync(
        Guid? competitorId,
        string competitorName,
        PainCluster cluster,
        List<SourceItem> current,
        List<SourceItem> previous,
        DateTime asOf,
        CancellationToken cancellationToken)
    {
        var currentCount = current.Count;
        var previousCount = previous.Count;
        var limited = currentCount < LimitedEvidenceThreshold;

        int? delta = null;
        decimal? percent = null;
        if (previousCount > 0 && currentCount > 0)
        {
            delta = currentCount - previousCount;
            percent = Math.Round((decimal)(currentCount - previousCount) / previousCount, 4, MidpointRounding.ToEven);
        }

        string classification;
        if (previousCount == 0 || currentCount == 0 || limited)
        {
            classification = "InsufficientData";
        }
        else if (percent >= 0.5m)
        {
            classification = "Emerging";
        }
        else if (percent <= -0.5m)
        {
            classification = "Declining";
        }
        else
        {
            classification = "Stable";
        }

        decimal totalConfidence = 0m;
        var confidenceSamples = 0;
        var ranked = new List<(SourceItem item, decimal confidence)>(currentCount);
        foreach (var item in current)
        {
            var analysis = await _analyses.LatestForSourceAsync(item.Id, cancellationToken).ConfigureAwait(false);
            var confidence = analysis is { Status: AnalysisStatus.Completed } ? analysis.Confidence : 0m;
            if (analysis is { Status: AnalysisStatus.Completed })
            {
                totalConfidence += confidence;
                confidenceSamples += 1;
            }
            ranked.Add((item, confidence));
        }
        var mean = confidenceSamples == 0
            ? 0m
            : Math.Round(totalConfidence / confidenceSamples, 4, MidpointRounding.ToEven);

        var representative = ranked
            .OrderByDescending(r => r.confidence)
            .ThenByDescending(r => r.item.EffectiveDateUtc)
            .Take(MaxRepresentative)
            .Select(r => new CompetitorGapEvidenceRef
            {
                SourceItemId = r.item.Id,
                CanonicalUrl = r.item.CanonicalUrl,
                Title = r.item.Title,
            }).ToList();

        var first = current.Count == 0 ? null : (DateTime?)current.Min(s => s.EffectiveDateUtc);
        var last = current.Count == 0 ? null : (DateTime?)current.Max(s => s.EffectiveDateUtc);

        return new CompetitorGapCell
        {
            CompetitorId = competitorId,
            CompetitorName = competitorName,
            ClusterId = cluster.Id,
            ClusterLabel = cluster.Label,
            EvidenceCount = currentCount,
            SourceCount = current.Select(s => s.CanonicalUrl).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            FirstObservedUtc = first,
            LastObservedUtc = last,
            PreviousCount = previousCount,
            Delta = delta,
            PercentChange = percent,
            Classification = classification,
            LimitedEvidence = limited,
            ConfidenceMean = mean,
            ConfidenceSampleCount = confidenceSamples,
            RepresentativeEvidence = representative,
            Stale = last.HasValue && IsStaleAfter(last.Value, asOf),
        };
    }

    /// <summary>
    /// Freshness policy for the matrix: evidence whose latest observation in the
    /// cell is more than 90 days old counts as stale. Counts are kept; only the
    /// label changes.
    /// </summary>
    private static bool IsStale(SourceItem item, DateTime asOf) => IsStaleAfter(item.EffectiveDateUtc, asOf);

    private static bool IsStaleAfter(DateTime timestamp, DateTime asOf) => (asOf - timestamp).TotalDays > 90;

    private static CompetitorResponse ToResponse(Competitor c) => new()
    {
        Id = c.Id,
        ProjectId = c.ProjectId,
        Name = c.Name,
        CreatedUtc = c.CreatedUtc,
    };
}

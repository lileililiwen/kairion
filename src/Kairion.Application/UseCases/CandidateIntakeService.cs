using System.Collections.Concurrent;
using System.Text.Json;
using Kairion.Application.Abstractions;
using Kairion.Application.Dtos;
using Kairion.Domain;

namespace Kairion.Application.UseCases;

/// <summary>
/// Source intake: accepts manual URLs and runs configured source providers. Idempotency
/// is enforced on (project, provider, external_id) and a secondary canonical URL hint;
/// duplicates surface the existing source item so the caller can render an evidence link.
/// </summary>
public sealed class CandidateIntakeService
{
    private readonly IResearchProjectRepository _projects;
    private readonly ISourceItemRepository _sourceItems;
    private readonly IObservationRepository _observations;
    private readonly ISourceIngestionRunRepository _runs;
    private readonly IProviderRegistry _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IJobScheduler _jobs;

    /// <summary>
    /// Per-project ingestion gate. Enforces the per-provider concurrency limit
    /// of 1 at the application layer so concurrent duplicate runs collapse to
    /// one candidate row even on stores without deferrable unique constraints.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    public CandidateIntakeService(
        IResearchProjectRepository projects,
        ISourceItemRepository sourceItems,
        IObservationRepository observations,
        ISourceIngestionRunRepository runs,
        IProviderRegistry providers,
        IUnitOfWork unitOfWork,
        IClock clock,
        IJobScheduler jobs)
    {
        _projects = projects;
        _sourceItems = sourceItems;
        _observations = observations;
        _runs = runs;
        _providers = providers;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _jobs = jobs;
    }

    public async Task<Result<SourceItemResponse>> ImportManualAsync(
        Guid projectId,
        ImportCandidateRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null) return Result<SourceItemResponse>.Failure($"Research project {projectId} not found.");

        try
        {
            ValidateImport(request);
        }
        catch (DomainValidationException ex)
        {
            return Result<SourceItemResponse>.Failure(ex.Message);
        }

        var existing = await _sourceItems
            .FindByProviderIdentityAsync(projectId, request.ProviderId, request.ExternalId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Result<SourceItemResponse>.Success(ToResponse(existing));
        }

        var canonicalExisting = await _sourceItems
            .FindByCanonicalUrlAsync(projectId, request.CanonicalUrl, cancellationToken)
            .ConfigureAwait(false);
        if (canonicalExisting is not null)
        {
            return Result<SourceItemResponse>.Success(ToResponse(canonicalExisting));
        }

        var now = _clock.UtcNow;
        var observation = new ProviderObservation(
            providerId: request.ProviderId,
            status: ProviderStatus.Available,
            errorCode: "manual_import",
            message: "Imported via manual URL intake.",
            observedUtc: now);

        var sourceItem = new SourceItem(
            id: Guid.NewGuid(),
            projectId: projectId,
            providerId: request.ProviderId,
            externalId: request.ExternalId,
            canonicalUrl: request.CanonicalUrl,
            title: request.Title,
            excerpt: request.Excerpt,
            publishedUtc: request.PublishedUtc,
            observedUtc: now,
            provenanceJson: request.ProvenanceJson ?? JsonSerializer.Serialize(new
            {
                source = "manual_import",
                importedAtUtc = now,
            }),
            latestObservation: observation);

        await _sourceItems.AddAsync(sourceItem, cancellationToken).ConfigureAwait(false);
        await _observations.AddAsync(new Observation(
            id: Guid.NewGuid(),
            projectId: projectId,
            sourceItemId: sourceItem.Id,
            clusterId: null,
            observedUtc: sourceItem.EffectiveDateUtc,
            firstObservedUtc: now), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _jobs.EnqueueAsync(
            idempotencyKey: $"screen:{sourceItem.Id}",
            jobName: "screening",
            handler: ct => Task.CompletedTask,
            cancellationToken).ConfigureAwait(false);

        return Result<SourceItemResponse>.Success(ToResponse(sourceItem));
    }

    public async Task<Result<IReadOnlyList<SourceItemResponse>>> RunSourceQueryAsync(
        Guid projectId,
        string providerId,
        RunSourceQueryRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null) return Result<IReadOnlyList<SourceItemResponse>>.Failure($"Research project {projectId} not found.");

        var provider = _providers.FindSourceProvider(providerId);
        if (provider is null)
        {
            return Result<IReadOnlyList<SourceItemResponse>>.Failure(
                $"Source provider '{providerId}' is not configured. Available providers: {string.Join(", ", _providers.ListSourceProviders().Select(p => p.ProviderId))}.");
        }

        var runId = Guid.NewGuid();
        if (!IsProviderEnabled(project, provider.ProviderId))
        {
            await PersistRunAsync(projectId, runId, provider.ProviderId, SourceRunStatus.Disabled, 0, "disabled", null, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<SourceItemResponse>>.Success(Array.Empty<SourceItemResponse>());
        }

        var settings = project.ProviderSettings.FirstOrDefault(s =>
            string.Equals(s.ProviderId, provider.ProviderId, StringComparison.OrdinalIgnoreCase));
        var maxResults = settings?.MaxResultsPerQuery ?? BoundMaxResults(request.MaxResults);
        var maxQueries = settings?.MaxQueries ?? SourceProviderLimits.MaxQueriesPerRun;

        var query = new SourceQuery(
            projectId: projectId,
            text: request.Text,
            topics: request.Topics,
            competitors: project.SourceConfiguration.IncludedCompetitors,
            windowStartUtc: project.SourceConfiguration.WindowStartUtc,
            windowEndUtc: project.SourceConfiguration.WindowEndUtc,
            maxResults: maxResults,
            maxQueries: maxQueries);

        SourceBatch batch;
        try
        {
            batch = await provider.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await PersistRunAsync(projectId, runId, provider.ProviderId, SourceRunStatus.Failed, 0, $"adapter_exception_{ex.GetType().Name}", null, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<SourceItemResponse>>.Failure(
                $"source_provider_error:{providerId}:{ex.GetType().Name}");
        }

        var persisted = await WithProjectGateAsync(
            projectId,
            async () =>
            {
                var items = await PersistBatchAsync(projectId, runId, batch, cancellationToken).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return items;
            },
            cancellationToken).ConfigureAwait(false);
        return Result<IReadOnlyList<SourceItemResponse>>.Success(persisted);
    }

    /// <summary>
    /// Runs every enabled provider for the project. One provider failure never
    /// discards another provider's accepted candidates; each provider gets its
    /// own run row sharing the same run id.
    /// </summary>
    public async Task<Result<IReadOnlyList<SourceItemResponse>>> CollectFromEnabledProvidersAsync(
        Guid projectId,
        string text,
        IReadOnlyList<string> topics,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null) return Result<IReadOnlyList<SourceItemResponse>>.Failure($"Research project {projectId} not found.");
        var runId = Guid.NewGuid();
        var all = new List<SourceItemResponse>();
        var enabledIds = project.SourceConfiguration.EnabledSourceProviderIds;
        if (enabledIds.Count == 0)
        {
            return Result<IReadOnlyList<SourceItemResponse>>.Success(all);
        }
        foreach (var id in enabledIds)
        {
            var provider = _providers.FindSourceProvider(id);
            if (provider is null)
            {
                await PersistRunAsync(projectId, runId, id, SourceRunStatus.Failed, 0, "unknown_provider", null, cancellationToken).ConfigureAwait(false);
                continue;
            }
            var settings = project.ProviderSettings.FirstOrDefault(s =>
                string.Equals(s.ProviderId, provider.ProviderId, StringComparison.OrdinalIgnoreCase));
            var query = new SourceQuery(
                projectId, text, topics ?? Array.Empty<string>(),
                project.SourceConfiguration.IncludedCompetitors,
                project.SourceConfiguration.WindowStartUtc,
                project.SourceConfiguration.WindowEndUtc,
                settings?.MaxResultsPerQuery ?? BoundMaxResults(maxResults),
                settings?.MaxQueries ?? SourceProviderLimits.MaxQueriesPerRun);
            SourceBatch batch;
            try
            {
                batch = await provider.SearchAsync(query, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                await PersistRunAsync(projectId, runId, provider.ProviderId, SourceRunStatus.Failed, 0, $"adapter_exception_{ex.GetType().Name}", null, cancellationToken).ConfigureAwait(false);
                continue;
            }
            var persisted = await PersistBatchAsync(projectId, runId, batch, cancellationToken).ConfigureAwait(false);
            all.AddRange(persisted);
        }
        await WithProjectGateAsync(
            projectId,
            async () =>
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        return Result<IReadOnlyList<SourceItemResponse>>.Success(all);
    }
    public async Task<IReadOnlyList<SourceIngestionRunResponse>> ListRunsAsync(
        Guid projectId, CancellationToken cancellationToken)
    {
        var runs = await _runs.ListForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        return runs.Select(r => new SourceIngestionRunResponse
        {
            Id = r.Id,
            ProjectId = r.ProjectId,
            RunId = r.RunId,
            ProviderId = r.ProviderId,
            Status = r.Status.ToString(),
            CandidateCount = r.CandidateCount,
            DiagnosticCode = r.DiagnosticCode,
            RetrievedAtUtc = r.RetrievedAtUtc,
            RetryAfterUtc = r.RetryAfterUtc,
        }).ToList();
    }

    private async Task<List<SourceItemResponse>> PersistBatchAsync(
        Guid projectId, Guid runId, SourceBatch batch, CancellationToken cancellationToken)
    {
        return await PersistBatchCoreAsync(projectId, runId, batch, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<SourceItemResponse>> PersistBatchCoreAsync(
        Guid projectId, Guid runId, SourceBatch batch, CancellationToken cancellationToken)
    {
        var persisted = new List<SourceItemResponse>(batch.Candidates.Count);
        foreach (var item in batch.Candidates)
        {
            // Provider-classified denials never produce a source item.
            if (item.Observation.Status is ProviderStatus.PolicyDenied
                or ProviderStatus.Unauthorized
                or ProviderStatus.RateLimited)
            {
                continue;
            }
            if (!CandidateNormalizer.TryNormalize(item, out var normalized, out _))
            {
                continue;
            }

            var existing = await _sourceItems
                .FindByProviderIdentityAsync(projectId, item.ProviderId, normalized!.ExternalId, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                existing.RecordObservation(item.Observation, normalized.ProvenanceJson);
                if (!string.Equals(existing.CanonicalUrl, normalized.CanonicalUrl, StringComparison.Ordinal))
                {
                    existing.CanonicalUrl = normalized.CanonicalUrl;
                }
                persisted.Add(ToResponse(existing));
                continue;
            }

            var canonicalExisting = await _sourceItems
                .FindByCanonicalUrlAsync(projectId, normalized.CanonicalUrl, cancellationToken)
                .ConfigureAwait(false);
            if (canonicalExisting is not null)
            {
                canonicalExisting.RecordObservation(item.Observation, normalized.ProvenanceJson);
                persisted.Add(ToResponse(canonicalExisting));
                continue;
            }

            var sourceItem = new SourceItem(
                id: Guid.NewGuid(),
                projectId: projectId,
                providerId: item.ProviderId,
                externalId: normalized.ExternalId,
                canonicalUrl: normalized.CanonicalUrl,
                title: normalized.Title,
                excerpt: normalized.Excerpt,
                publishedUtc: item.PublishedUtc,
                observedUtc: item.ObservedUtc,
                provenanceJson: normalized.ProvenanceJson,
                latestObservation: item.Observation);
            await _sourceItems.AddAsync(sourceItem, cancellationToken).ConfigureAwait(false);
            await _observations.AddAsync(new Observation(
                id: Guid.NewGuid(),
                projectId: projectId,
                sourceItemId: sourceItem.Id,
                clusterId: null,
                observedUtc: sourceItem.EffectiveDateUtc,
                firstObservedUtc: _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            persisted.Add(ToResponse(sourceItem));
        }

        var status = batch.Status;
        if (status == SourceRunStatus.Complete && persisted.Count < batch.Candidates.Count)
        {
            status = SourceRunStatus.Partial;
        }
        await PersistRunAsync(projectId, runId, batch.ProviderId, status, persisted.Count, batch.DiagnosticCode, batch.RetryAfterUtc, cancellationToken).ConfigureAwait(false);
        return persisted;
    }

    private Task PersistRunAsync(
        Guid projectId, Guid runId, string providerId, SourceRunStatus status,
        int candidateCount, string diagnosticCode, DateTime? retryAfterUtc,
        CancellationToken cancellationToken) =>
        _runs.AddAsync(new SourceIngestionRun(
            Guid.NewGuid(), projectId, runId, providerId, status,
            candidateCount, diagnosticCode, _clock.UtcNow, retryAfterUtc), cancellationToken);

    private static bool IsProviderEnabled(ResearchProject project, string providerId)
    {
        var enabled = project.SourceConfiguration.EnabledSourceProviderIds;
        if (enabled.Count == 0)
        {
            // No explicit configuration: manual/demo-style providers stay usable so
            // existing clients keep working; policy-gated adapters default disabled
            // via their own IsAvailableAsync/Disabled batch.
            return true;
        }
        return enabled.Contains(providerId, StringComparer.OrdinalIgnoreCase);
    }

    private static int BoundMaxResults(int requested) =>
        SourceProviderLimits.BoundResultsPerQuery(requested);

    private static async Task<T> WithProjectGateAsync<T>(
        Guid projectId, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<SourceItemResponse>> ListForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var items = await _sourceItems.ListForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        return items.Select(ToResponse).ToList();
    }

    private static void ValidateImport(ImportCandidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderId))
        {
            throw new DomainValidationException("providerId is required.");
        }
        if (string.IsNullOrWhiteSpace(request.ExternalId))
        {
            throw new DomainValidationException("externalId is required.");
        }
        if (string.IsNullOrWhiteSpace(request.CanonicalUrl))
        {
            throw new DomainValidationException("canonicalUrl is required.");
        }
        request.CanonicalUrl = CanonicalUrl.Normalize(request.CanonicalUrl);
    }

    internal static SourceItemResponse ToResponse(SourceItem item)
    {
        return new SourceItemResponse
        {
            Id = item.Id,
            ProjectId = item.ProjectId,
            ProviderId = item.ProviderId,
            ExternalId = item.ExternalId,
            CanonicalUrl = item.CanonicalUrl,
            Title = item.Title,
            Excerpt = item.Excerpt,
            PublishedUtc = item.PublishedUtc,
            ObservedUtc = item.ObservedUtc,
            EffectiveDateUtc = item.EffectiveDateUtc,
            ProviderStatus = item.LatestObservation.Status.ToString(),
            DuplicateOfId = item.DuplicateOfId,
        };
    }
}

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
    private readonly IProviderRegistry _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IJobScheduler _jobs;

    public CandidateIntakeService(
        IResearchProjectRepository projects,
        ISourceItemRepository sourceItems,
        IObservationRepository observations,
        IProviderRegistry providers,
        IUnitOfWork unitOfWork,
        IClock clock,
        IJobScheduler jobs)
    {
        _projects = projects;
        _sourceItems = sourceItems;
        _observations = observations;
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

        var query = new SourceQuery(
            projectId: projectId,
            text: request.Text,
            topics: request.Topics,
            competitors: project.SourceConfiguration.IncludedCompetitors,
            windowStartUtc: project.SourceConfiguration.WindowStartUtc,
            windowEndUtc: project.SourceConfiguration.WindowEndUtc,
            maxResults: request.MaxResults);

        IReadOnlyList<SourceFetchResult> fetched;
        try
        {
            fetched = await provider.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<SourceItemResponse>>.Failure(
                $"source_provider_error:{providerId}:{ex.GetType().Name}");
        }

        var persisted = new List<SourceItemResponse>(fetched.Count);
        foreach (var item in fetched)
        {
            // Provider-classified denials never produce a source item, but we still
            // return a "candidate" with the observation so the UI can show the failure.
            if (item.Observation.Status is ProviderStatus.PolicyDenied
                or ProviderStatus.Unauthorized
                or ProviderStatus.RateLimited)
            {
                continue;
            }

            var existing = await _sourceItems
                .FindByProviderIdentityAsync(projectId, item.ProviderId, item.ExternalId, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                existing.RecordObservation(item.Observation, item.ProvenanceJson);
                persisted.Add(ToResponse(existing));
                continue;
            }

            var canonicalExisting = await _sourceItems
                .FindByCanonicalUrlAsync(projectId, item.CanonicalUrl, cancellationToken)
                .ConfigureAwait(false);
            if (canonicalExisting is not null)
            {
                persisted.Add(ToResponse(canonicalExisting));
                continue;
            }

            var sourceItem = new SourceItem(
                id: Guid.NewGuid(),
                projectId: projectId,
                providerId: item.ProviderId,
                externalId: item.ExternalId,
                canonicalUrl: item.CanonicalUrl,
                title: item.Title,
                excerpt: item.Excerpt,
                publishedUtc: item.PublishedUtc,
                observedUtc: item.ObservedUtc,
                provenanceJson: item.ProvenanceJson,
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

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<IReadOnlyList<SourceItemResponse>>.Success(persisted);
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
        if (!Uri.TryCreate(request.CanonicalUrl, UriKind.Absolute, out _))
        {
            throw new DomainValidationException("canonicalUrl must be an absolute URL.");
        }
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

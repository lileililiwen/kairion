using Kairion.Application.Abstractions;
using Kairion.Application.Dtos;
using Kairion.Domain;

namespace Kairion.Application.UseCases;

/// <summary>
/// Create / read / update / archive operations on a research project. The service owns
/// the validation rules and the persistence call; the controller layer is responsible
/// for translating results to HTTP responses.
/// </summary>
public sealed class ResearchProjectService
{
    private readonly IResearchProjectRepository _projects;
    private readonly ICompetitorRepository _competitors;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IProviderRegistry _providers;

    public ResearchProjectService(
        IResearchProjectRepository projects,
        ICompetitorRepository competitors,
        IUnitOfWork unitOfWork,
        IClock clock,
        IProviderRegistry providers)
    {
        _projects = projects;
        _competitors = competitors;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _providers = providers;
    }

    public async Task<Result<ResearchProjectResponse>> CreateAsync(
        CreateResearchProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            ValidateCreate(request);
            var providerSettings = BuildProviderSettings(request.ProviderConfigs, request.EnabledSourceProviderIds);
            var now = _clock.UtcNow;
            var project = new ResearchProject(
                id: Guid.NewGuid(),
                title: request.Title,
                briefKind: request.BriefKind,
                briefText: request.BriefText,
                topics: request.Topics ?? new List<string>(),
                sourceConfiguration: BuildSourceConfiguration(request, providerSettings),
                createdUtc: now,
                providerSettings: providerSettings);
            await _projects.AddAsync(project, cancellationToken).ConfigureAwait(false);
            await SyncCompetitorsAsync(project, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<ResearchProjectResponse>.Success(ToResponse(project));
        }
        catch (DomainValidationException ex)
        {
            return Result<ResearchProjectResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<ResearchProjectResponse>> UpdateAsync(
        Guid projectId,
        UpdateResearchProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return Result<ResearchProjectResponse>.Failure($"Research project {projectId} not found.");
        }
        if (project.State == ResearchProjectState.Archived)
        {
            return Result<ResearchProjectResponse>.Failure("Archived projects cannot be edited; restore first.");
        }
        try
        {
            ValidateUpdate(request);
            var providerSettings = BuildProviderSettings(request.ProviderConfigs, request.EnabledSourceProviderIds);
            project.Update(
                title: request.Title,
                briefKind: request.BriefKind,
                briefText: request.BriefText,
                topics: request.Topics ?? new List<string>(),
                sourceConfiguration: BuildSourceConfiguration(request, providerSettings),
                updatedUtc: _clock.UtcNow,
                providerSettings: providerSettings);
            await SyncCompetitorsAsync(project, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<ResearchProjectResponse>.Success(ToResponse(project));
        }
        catch (DomainValidationException ex)
        {
            return Result<ResearchProjectResponse>.Failure(ex.Message);
        }
    }

    public async Task<ResearchProjectResponse?> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        return project is null ? null : ToResponse(project);
    }

    public async Task<IReadOnlyList<ResearchProjectResponse>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var projects = await _projects.ListAsync(includeArchived, cancellationToken).ConfigureAwait(false);
        return projects.Select(ToResponse).ToList();
    }

    public async Task<Result<ResearchProjectResponse>> ArchiveAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null) return Result<ResearchProjectResponse>.Failure($"Research project {projectId} not found.");
        if (project.State == ResearchProjectState.Archived)
        {
            return Result<ResearchProjectResponse>.Success(ToResponse(project));
        }
        project.Archive(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<ResearchProjectResponse>.Success(ToResponse(project));
    }

    public async Task<Result<ResearchProjectResponse>> RestoreAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null) return Result<ResearchProjectResponse>.Failure($"Research project {projectId} not found.");
        if (project.State == ResearchProjectState.Active)
        {
            return Result<ResearchProjectResponse>.Success(ToResponse(project));
        }
        project.Restore(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<ResearchProjectResponse>.Success(ToResponse(project));
    }

    private static void ValidateCreate(CreateResearchProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new DomainValidationException("title is required.");
        }
        ValidateBrief(request.BriefText);
    }

    private static void ValidateUpdate(UpdateResearchProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new DomainValidationException("title is required.");
        }
        ValidateBrief(request.BriefText);
    }

    private static void ValidateBrief(string brief)
    {
        if (string.IsNullOrWhiteSpace(brief))
        {
            throw new DomainValidationException("briefText is required and must contain non-whitespace characters.");
        }
    }

    private static SourceConfiguration BuildSourceConfiguration(CreateResearchProjectRequest request, IReadOnlyList<SourceProviderSettings> providerSettings)
    {
        var enabled = providerSettings.Count > 0
            ? providerSettings.Where(s => s.Enabled).Select(s => s.ProviderId).ToList()
            : (request.EnabledSourceProviderIds ?? new List<string>());
        return new SourceConfiguration(
            enabledSourceProviderIds: enabled,
            includedCompetitors: request.IncludedCompetitors ?? new List<string>(),
            queryStrategy: request.QueryStrategy,
            windowStartUtc: request.WindowStartUtc,
            windowEndUtc: request.WindowEndUtc);
    }

    private static SourceConfiguration BuildSourceConfiguration(UpdateResearchProjectRequest request, IReadOnlyList<SourceProviderSettings> providerSettings)
    {
        var enabled = providerSettings.Count > 0
            ? providerSettings.Where(s => s.Enabled).Select(s => s.ProviderId).ToList()
            : (request.EnabledSourceProviderIds ?? new List<string>());
        return new SourceConfiguration(
            enabledSourceProviderIds: enabled,
            includedCompetitors: request.IncludedCompetitors ?? new List<string>(),
            queryStrategy: request.QueryStrategy,
            windowStartUtc: request.WindowStartUtc,
            windowEndUtc: request.WindowEndUtc);
    }

    private IReadOnlyList<SourceProviderSettings> BuildProviderSettings(
        List<SourceProviderConfigRequest>? configs,
        List<string>? legacyEnabledIds)
    {
        var known = _providers.ListSourceProviders().Select(p => p.ProviderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<SourceProviderSettings>();
        if (configs is not null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in configs)
            {
                if (string.IsNullOrWhiteSpace(c.ProviderId))
                {
                    throw new DomainValidationException("providerConfigs.providerId is required.");
                }
                var key = c.ProviderId.Trim();
                if (!known.Contains(key))
                {
                    throw new DomainValidationException($"Unknown source provider '{key}'. Available: {string.Join(", ", known.OrderBy(k => k))}.");
                }
                if (!seen.Add(key))
                {
                    throw new DomainValidationException($"Duplicate provider config '{key}'.");
                }
                if (!string.IsNullOrWhiteSpace(c.CredentialRef) && c.CredentialRef.Trim().Length > 200)
                {
                    throw new DomainValidationException("credentialRef must be 200 characters or fewer and must not contain a secret.");
                }
                // SourceProviderSettings constructor enforces 1..5 / 1..50 and HTTPS.
                result.Add(new SourceProviderSettings(
                    key, c.Enabled, c.MaxQueries, c.MaxResultsPerQuery, c.Endpoint, c.CredentialRef));
            }
        }
        if (legacyEnabledIds is not null)
        {
            foreach (var id in legacyEnabledIds.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var key = id.Trim();
                if (!known.Contains(key))
                {
                    throw new DomainValidationException($"Unknown source provider '{key}'. Available: {string.Join(", ", known.OrderBy(k => k))}.");
                }
                if (result.All(s => !string.Equals(s.ProviderId, key, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(new SourceProviderSettings(key, enabled: true));
                }
            }
        }
        return result;
    }

    private async Task SyncCompetitorsAsync(ResearchProject project, CancellationToken cancellationToken)
    {
        var existing = await _competitors.ListForProjectAsync(project.Id, cancellationToken).ConfigureAwait(false);
        var known = existing.Select(c => c.NormalizedName).ToHashSet(StringComparer.Ordinal);
        foreach (var raw in project.IncludedCompetitors ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var name = raw.Trim();
            if (name.Length > 200) continue;
            if (!known.Add(Competitor.Normalize(name))) continue;
            await _competitors.AddAsync(new Competitor(Guid.NewGuid(), project.Id, name, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        }
    }

    internal static ResearchProjectResponse ToResponse(ResearchProject project)
    {
        return new ResearchProjectResponse
        {
            Id = project.Id,
            Title = project.Title,
            BriefKind = project.BriefKind,
            BriefText = project.BriefText,
            Topics = project.Topics.ToList(),
            IncludedCompetitors = project.SourceConfiguration.IncludedCompetitors.ToList(),
            EnabledSourceProviderIds = project.SourceConfiguration.EnabledSourceProviderIds.ToList(),
            ProviderConfigs = project.ProviderSettings.Select(s => new SourceProviderConfigResponse
            {
                ProviderId = s.ProviderId,
                Enabled = s.Enabled,
                MaxQueries = s.MaxQueries,
                MaxResultsPerQuery = s.MaxResultsPerQuery,
                Endpoint = s.Endpoint,
                CredentialRef = s.CredentialRef,
            }).ToList(),
            QueryStrategy = project.SourceConfiguration.QueryStrategy,
            WindowStartUtc = project.SourceConfiguration.WindowStartUtc,
            WindowEndUtc = project.SourceConfiguration.WindowEndUtc,
            State = project.State,
            CreatedUtc = project.CreatedUtc,
            UpdatedUtc = project.UpdatedUtc,
            ArchivedUtc = project.ArchivedUtc,
        };
    }
}

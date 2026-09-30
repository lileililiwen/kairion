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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ResearchProjectService(
        IResearchProjectRepository projects,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _projects = projects;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ResearchProjectResponse>> CreateAsync(
        CreateResearchProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            ValidateCreate(request);
            var now = _clock.UtcNow;
            var project = new ResearchProject(
                id: Guid.NewGuid(),
                title: request.Title,
                briefKind: request.BriefKind,
                briefText: request.BriefText,
                topics: request.Topics ?? new List<string>(),
                sourceConfiguration: BuildSourceConfiguration(request),
                createdUtc: now);
            await _projects.AddAsync(project, cancellationToken).ConfigureAwait(false);
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
            project.Update(
                title: request.Title,
                briefKind: request.BriefKind,
                briefText: request.BriefText,
                topics: request.Topics ?? new List<string>(),
                sourceConfiguration: BuildSourceConfiguration(request),
                updatedUtc: _clock.UtcNow);
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

    private static SourceConfiguration BuildSourceConfiguration(CreateResearchProjectRequest request)
    {
        return new SourceConfiguration(
            enabledSourceProviderIds: request.EnabledSourceProviderIds ?? new List<string>(),
            includedCompetitors: request.IncludedCompetitors ?? new List<string>(),
            queryStrategy: request.QueryStrategy,
            windowStartUtc: request.WindowStartUtc,
            windowEndUtc: request.WindowEndUtc);
    }

    private static SourceConfiguration BuildSourceConfiguration(UpdateResearchProjectRequest request)
    {
        return new SourceConfiguration(
            enabledSourceProviderIds: request.EnabledSourceProviderIds ?? new List<string>(),
            includedCompetitors: request.IncludedCompetitors ?? new List<string>(),
            queryStrategy: request.QueryStrategy,
            windowStartUtc: request.WindowStartUtc,
            windowEndUtc: request.WindowEndUtc);
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

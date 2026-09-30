using Kairion.Application.Abstractions;
using Kairion.Application.Dtos;
using Kairion.Application.Schemas;
using Kairion.Domain;

namespace Kairion.Application.UseCases;

/// <summary>
/// Two-stage AI pipeline: cheap screening precedes deep analysis, and deep analysis is
/// skipped (or marked failed) when the screening call fails. Every AI result is validated
/// against a versioned JSON schema before persistence. Provider failures are recorded and
/// remain visible; source evidence is never deleted.
/// </summary>
public sealed class AnalysisOrchestrator
{
    private readonly ISourceItemRepository _sourceItems;
    private readonly IScreeningResultRepository _screenings;
    private readonly IDeepAnalysisRepository _analyses;
    private readonly IObservationRepository _observations;
    private readonly IProviderRegistry _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly AiSchemaValidator _schemaValidator;
    private readonly ScreeningDecisionPolicy _decisionPolicy;

    public AnalysisOrchestrator(
        ISourceItemRepository sourceItems,
        IScreeningResultRepository screenings,
        IDeepAnalysisRepository analyses,
        IObservationRepository observations,
        IProviderRegistry providers,
        IUnitOfWork unitOfWork,
        IClock clock,
        AiSchemaValidator schemaValidator,
        ScreeningDecisionPolicy decisionPolicy)
    {
        _sourceItems = sourceItems;
        _screenings = screenings;
        _analyses = analyses;
        _observations = observations;
        _providers = providers;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _schemaValidator = schemaValidator;
        _decisionPolicy = decisionPolicy;
    }

    public async Task<Result<ScreeningOutcomeResponse>> ScreenAsync(
        Guid sourceItemId,
        string aiProviderId,
        CancellationToken cancellationToken)
    {
        var source = await _sourceItems.FindAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        if (source is null) return Result<ScreeningOutcomeResponse>.Failure($"Source item {sourceItemId} not found.");
        var provider = _providers.FindAiProvider(aiProviderId);
        if (provider is null) return Result<ScreeningOutcomeResponse>.Failure($"AI provider '{aiProviderId}' is not configured.");

        var now = _clock.UtcNow;
        var input = new
        {
            title = source.Title,
            excerpt = source.Excerpt,
            canonicalUrl = source.CanonicalUrl,
            providerId = source.ProviderId,
            observedUtc = source.ObservedUtc,
        };
        AiResult<ScreeningAiResponse> ai;
        try
        {
            var raw = await provider.AnalyzeAsync<ScreeningAiResponse>(input, AiSchemas.ScreeningV1, cancellationToken).ConfigureAwait(false);
            var validated = _schemaValidator.ValidateAndDeserialize<ScreeningAiResponse>(Serialize(raw.Output), AiSchemas.ScreeningV1);
            // Re-stamp metadata with the real provider/model. If the validator
            // rejected the response, preserve its null output and failure code
            // so the caller sees a properly typed failure.
            var validatedOutput = validated.Output;
            ai = new AiResult<ScreeningAiResponse>(
                validatedOutput,
                new AiCallMetadata(
                    providerId: provider.ProviderId,
                    model: validated.Metadata.Model == "n/a" ? "provider-returned" : validated.Metadata.Model,
                    schemaVersion: AiSchemas.ScreeningV1.Version,
                    status: validated.Metadata.Status,
                    failureCode: validated.Metadata.FailureCode,
                    failureMessage: validated.Metadata.FailureMessage,
                    promptTokens: validated.Metadata.PromptTokens,
                    completionTokens: validated.Metadata.CompletionTokens,
                    calledAtUtc: now));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var failed = ScreeningResult.Failure(
                id: Guid.NewGuid(),
                sourceItemId: source.Id,
                analysisVersion: AiSchemas.ScreeningV1.Version,
                providerId: provider.ProviderId,
                model: "n/a",
                failureReason: $"ai_error:{ex.GetType().Name}",
                createdUtc: now);
            await _screenings.AddAsync(failed, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<ScreeningOutcomeResponse>.Success(ToResponse(failed));
        }

        if (ai.Output is null)
        {
            var failed = ScreeningResult.Failure(
                id: Guid.NewGuid(),
                sourceItemId: source.Id,
                analysisVersion: AiSchemas.ScreeningV1.Version,
                providerId: provider.ProviderId,
                model: "n/a",
                failureReason: ai.Metadata.FailureCode ?? "schema_invalid",
                createdUtc: now);
            await _screenings.AddAsync(failed, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<ScreeningOutcomeResponse>.Success(ToResponse(failed));
        }

        var decision = _decisionPolicy.Decide(ai.Output);
        var screening = new ScreeningResult(
            id: Guid.NewGuid(),
            sourceItemId: source.Id,
            analysisVersion: AiSchemas.ScreeningV1.Version,
            relevance: ai.Output.Relevance,
            pain: ai.Output.Pain,
            commercialHint: ai.Output.CommercialHint,
            spam: ai.Output.Spam,
            decision: decision,
            providerId: provider.ProviderId,
            model: "provider-returned",
            createdUtc: now,
            failureReason: null);
        await _screenings.AddAsync(screening, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<ScreeningOutcomeResponse>.Success(ToResponse(screening));
    }

    public async Task<Result<DeepAnalysisDto>> AnalyzeAsync(
        Guid sourceItemId,
        string aiProviderId,
        CancellationToken cancellationToken)
    {
        var source = await _sourceItems.FindAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        if (source is null) return Result<DeepAnalysisDto>.Failure($"Source item {sourceItemId} not found.");
        var latestScreening = await _screenings.LatestForSourceAsync(source.Id, cancellationToken).ConfigureAwait(false);
        if (latestScreening is null)
        {
            return Result<DeepAnalysisDto>.Failure("Run screening before deep analysis.");
        }
        if (latestScreening.Decision != ScreeningDecision.Retain)
        {
            return Result<DeepAnalysisDto>.Failure(
                $"Source item {source.Id} was screened with decision '{latestScreening.Decision}'. Deep analysis is skipped.");
        }

        var provider = _providers.FindAiProvider(aiProviderId);
        if (provider is null) return Result<DeepAnalysisDto>.Failure($"AI provider '{aiProviderId}' is not configured.");

        var now = _clock.UtcNow;
        var input = new
        {
            title = source.Title,
            excerpt = source.Excerpt,
            canonicalUrl = source.CanonicalUrl,
            providerId = source.ProviderId,
            publishedUtc = source.PublishedUtc,
            observedUtc = source.ObservedUtc,
            screening = new
            {
                relevance = latestScreening.Relevance,
                pain = latestScreening.Pain,
                commercialHint = latestScreening.CommercialHint,
                decision = latestScreening.Decision.ToString(),
            },
        };
        DeepAnalysis analysis;
        try
        {
            var raw = await provider.AnalyzeAsync<DeepAnalysisAiResponse>(input, AiSchemas.DeepAnalysisV1, cancellationToken).ConfigureAwait(false);
            // The provider MUST validate the response against the schema; we re-run the
            // schema validator on the resulting JSON to make the contract explicit.
            var aiJson = Serialize(raw.Output);
            var validated = _schemaValidator.ValidateAndDeserialize<DeepAnalysisAiResponse>(aiJson, AiSchemas.DeepAnalysisV1);
            if (validated.Output is null)
            {
                analysis = DeepAnalysis.Failure(
                    id: Guid.NewGuid(),
                    sourceItemId: source.Id,
                    schemaVersion: AiSchemas.DeepAnalysisV1.Version,
                    providerId: provider.ProviderId,
                    model: "n/a",
                    failureReason: validated.Metadata.FailureCode ?? "schema_invalid",
                    createdUtc: now);
                await _analyses.AddAsync(analysis, cancellationToken).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<DeepAnalysisDto>.Success(ToResponse(analysis));
            }
            var ok = validated.Output;
            analysis = new DeepAnalysis(
                id: Guid.NewGuid(),
                sourceItemId: source.Id,
                schemaVersion: AiSchemas.DeepAnalysisV1.Version,
                problem: ok.Problem,
                context: ok.Context,
                currentSolution: ok.CurrentSolution,
                dissatisfaction: ok.Dissatisfaction,
                workaround: ok.Workaround,
                desiredOutcome: ok.DesiredOutcome,
                category: ok.Category,
                priceSensitivity: ok.PriceSensitivity,
                painStrength: ok.PainStrength,
                confidence: ok.Confidence,
                providerId: provider.ProviderId,
                model: "provider-returned",
                status: AnalysisStatus.Completed,
                createdUtc: now);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            analysis = DeepAnalysis.Failure(
                id: Guid.NewGuid(),
                sourceItemId: source.Id,
                schemaVersion: AiSchemas.DeepAnalysisV1.Version,
                providerId: provider.ProviderId,
                model: "n/a",
                failureReason: $"ai_error:{ex.GetType().Name}",
                createdUtc: now);
        }
        await _analyses.AddAsync(analysis, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<DeepAnalysisDto>.Success(ToResponse(analysis));
    }

    public async Task<ScreeningOutcomeResponse?> LatestScreeningAsync(
        Guid sourceItemId,
        CancellationToken cancellationToken)
    {
        var screening = await _screenings.LatestForSourceAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        return screening is null ? null : ToResponse(screening);
    }

    public async Task<DeepAnalysisDto?> LatestAnalysisAsync(
        Guid sourceItemId,
        CancellationToken cancellationToken)
    {
        var analysis = await _analyses.LatestForSourceAsync(sourceItemId, cancellationToken).ConfigureAwait(false);
        return analysis is null ? null : ToResponse(analysis);
    }

    private static string Serialize<T>(T? value)
    {
        if (value is null) return "null";
        return System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
        {
            // Match the validator naming policy so the round-trip through the
            // schema validator keeps every property name aligned with the schema.
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        });
    }

    internal static ScreeningOutcomeResponse ToResponse(ScreeningResult s)
    {
        return new ScreeningOutcomeResponse
        {
            Id = s.Id,
            SourceItemId = s.SourceItemId,
            AnalysisVersion = s.AnalysisVersion,
            Relevance = s.Relevance,
            Pain = s.Pain,
            CommercialHint = s.CommercialHint,
            Spam = s.Spam,
            Decision = s.Decision,
            ProviderId = s.ProviderId,
            Model = s.Model,
            CreatedUtc = s.CreatedUtc,
            FailureReason = s.FailureReason,
        };
    }

    internal static DeepAnalysisDto ToResponse(DeepAnalysis a)
    {
        return new DeepAnalysisDto
        {
            Id = a.Id,
            SourceItemId = a.SourceItemId,
            SchemaVersion = a.SchemaVersion,
            Problem = a.Problem,
            Context = a.Context,
            CurrentSolution = a.CurrentSolution,
            Dissatisfaction = a.Dissatisfaction,
            Workaround = a.Workaround,
            DesiredOutcome = a.DesiredOutcome,
            Category = a.Category,
            PriceSensitivity = a.PriceSensitivity,
            PainStrength = a.PainStrength,
            Confidence = a.Confidence,
            Status = a.Status,
            ProviderId = a.ProviderId,
            Model = a.Model,
            CreatedUtc = a.CreatedUtc,
            FailureReason = a.FailureReason,
        };
    }
}

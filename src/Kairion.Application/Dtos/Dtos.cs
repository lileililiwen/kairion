using Kairion.Domain;

namespace Kairion.Application.Dtos;

// ---- Research Projects -------------------------------------------------------

public sealed class SourceProviderConfigRequest
{
    public string ProviderId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int MaxQueries { get; set; } = 5;
    public int MaxResultsPerQuery { get; set; } = 50;
    public string? Endpoint { get; set; }
    public string? CredentialRef { get; set; }
}

public sealed class SourceProviderConfigResponse
{
    public string ProviderId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int MaxQueries { get; set; }
    public int MaxResultsPerQuery { get; set; }
    public string? Endpoint { get; set; }
    public string? CredentialRef { get; set; }
}

public sealed class CreateResearchProjectRequest
{
    public string Title { get; set; } = string.Empty;
    public BriefKind BriefKind { get; set; } = BriefKind.Market;
    public string BriefText { get; set; } = string.Empty;
    public List<string> Topics { get; set; } = new();
    public List<string> IncludedCompetitors { get; set; } = new();
    public List<string> EnabledSourceProviderIds { get; set; } = new();
    public List<SourceProviderConfigRequest> ProviderConfigs { get; set; } = new();
    public string? QueryStrategy { get; set; }
    public DateTime? WindowStartUtc { get; set; }
    public DateTime? WindowEndUtc { get; set; }
}

public sealed class UpdateResearchProjectRequest
{
    public string Title { get; set; } = string.Empty;
    public BriefKind BriefKind { get; set; } = BriefKind.Market;
    public string BriefText { get; set; } = string.Empty;
    public List<string> Topics { get; set; } = new();
    public List<string> IncludedCompetitors { get; set; } = new();
    public List<string> EnabledSourceProviderIds { get; set; } = new();
    public List<SourceProviderConfigRequest> ProviderConfigs { get; set; } = new();
    public string? QueryStrategy { get; set; }
    public DateTime? WindowStartUtc { get; set; }
    public DateTime? WindowEndUtc { get; set; }
}

public sealed class ResearchProjectResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public BriefKind BriefKind { get; set; }
    public string BriefText { get; set; } = string.Empty;
    public List<string> Topics { get; set; } = new();
    public List<string> IncludedCompetitors { get; set; } = new();
    public List<string> EnabledSourceProviderIds { get; set; } = new();
    public List<SourceProviderConfigResponse> ProviderConfigs { get; set; } = new();
    public string? QueryStrategy { get; set; }
    public DateTime? WindowStartUtc { get; set; }
    public DateTime? WindowEndUtc { get; set; }
    public ResearchProjectState State { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? ArchivedUtc { get; set; }
}

// ---- Source Candidates -------------------------------------------------------
public sealed class SourceIngestionRunResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid RunId { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CandidateCount { get; set; }
    public string DiagnosticCode { get; set; } = string.Empty;
    public DateTime RetrievedAtUtc { get; set; }
    public DateTime? RetryAfterUtc { get; set; }
}

public sealed class ImportCandidateRequest
{
    public string ProviderId { get; set; } = "manual";
    public string ExternalId { get; set; } = string.Empty;
    public string CanonicalUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Excerpt { get; set; }
    public DateTime? PublishedUtc { get; set; }
    public string? ProvenanceJson { get; set; }
}

public sealed class RunSourceQueryRequest
{
    public string Text { get; set; } = string.Empty;
    public List<string> Topics { get; set; } = new();
    public int MaxResults { get; set; } = 25;
    public int MaxQueries { get; set; } = 5;
}

public sealed class SourceItemResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string CanonicalUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Excerpt { get; set; }
    public DateTime? PublishedUtc { get; set; }
    public DateTime ObservedUtc { get; set; }
    public DateTime EffectiveDateUtc { get; set; }
    public string ProviderStatus { get; set; } = string.Empty;
    public Guid? DuplicateOfId { get; set; }
}

// ---- Screening / Analysis ----------------------------------------------------

public sealed class ScreeningOutcomeResponse
{
    public Guid Id { get; set; }
    public Guid SourceItemId { get; set; }
    public string AnalysisVersion { get; set; } = string.Empty;
    public decimal Relevance { get; set; }
    public decimal Pain { get; set; }
    public decimal CommercialHint { get; set; }
    public bool Spam { get; set; }
    public ScreeningDecision Decision { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class DeepAnalysisDto
{
    public Guid Id { get; set; }
    public Guid SourceItemId { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string CurrentSolution { get; set; } = string.Empty;
    public string Dissatisfaction { get; set; } = string.Empty;
    public string Workaround { get; set; } = string.Empty;
    public string DesiredOutcome { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? PriceSensitivity { get; set; }
    public decimal PainStrength { get; set; }
    public decimal Confidence { get; set; }
    public AnalysisStatus Status { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string? FailureReason { get; set; }
}

// ---- Clusters ----------------------------------------------------------------

public sealed class ClusterResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int Version { get; set; }
    public int ReviewStateVersion { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public int EvidenceCount { get; set; }
    public decimal AverageConfidence { get; set; }
}

public sealed class ClusterEvidenceItem
{
    public Guid SourceItemId { get; set; }
    public string CanonicalUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Excerpt { get; set; }
    public DateTime? PublishedUtc { get; set; }
    public DateTime ObservedUtc { get; set; }
    public decimal Confidence { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public AssignmentOrigin AssignmentOrigin { get; set; }
    public DateTime AssignedAtUtc { get; set; }
}

public sealed class ClusterEvidenceResponse
{
    public Guid ClusterId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int ReviewStateVersion { get; set; }
    public List<ClusterEvidenceItem> Items { get; set; } = new();
}

public sealed class UpdateClusterRequest
{
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class ReassignItemRequest
{
    public Guid SourceItemId { get; set; }
    public Guid TargetClusterId { get; set; }
}

public sealed class MergeClustersRequest
{
    public Guid SourceClusterId { get; set; }
    public Guid TargetClusterId { get; set; }
}

public sealed class SplitClusterRequest
{
    public string NewClusterLabel { get; set; } = string.Empty;
    public string NewClusterCategory { get; set; } = string.Empty;
    public string NewClusterSummary { get; set; } = string.Empty;
    public List<Guid> SourceItemIds { get; set; } = new();
}

// ---- Trends / Opportunity Signals -------------------------------------------

public sealed class TrendResponse
{
    public Guid ProjectId { get; set; }
    public string Window { get; set; } = string.Empty;
    public DateTime AsOfUtc { get; set; }
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public int CurrentCount { get; set; }
    public int PreviousCount { get; set; }
    public decimal? PercentGrowth { get; set; }
    public bool NewSignal { get; set; }
    public string Label { get; set; } = string.Empty;
    public List<ClusterTrend> Clusters { get; set; } = new();
}

public sealed class ClusterTrend
{
    public Guid ClusterId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int CurrentCount { get; set; }
    public int PreviousCount { get; set; }
    public decimal? PercentGrowth { get; set; }
    public bool NewSignal { get; set; }
    public string LabelTrend { get; set; } = string.Empty;
}

public sealed class OpportunitySignalResponse
{
    public Guid Id { get; set; }
    public Guid ClusterId { get; set; }
    public int EvidenceVolume { get; set; }
    public decimal? PercentGrowth { get; set; }
    public bool NewSignal { get; set; }
    public List<string> CurrentAlternatives { get; set; } = new();
    public List<string> Workarounds { get; set; } = new();
    public decimal Confidence { get; set; }
    public string? AiExplanation { get; set; }
    public string TrendLabel { get; set; } = string.Empty;
    public string Window { get; set; } = string.Empty;
    public DateTime AsOfUtc { get; set; }
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public DateTime GeneratedUtc { get; set; }
    public string Disclosure { get; set; } = "This is an evidence signal, not business validation.";
    public List<Guid> RepresentativeSourceItemIds { get; set; } = new();
}

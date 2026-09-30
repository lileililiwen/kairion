/**
 * Strictly-typed API client for Kairion v1. The wire contract is the single source of
 * truth: if a new field is added to the API, the TypeScript type must be updated in the
 * same change. Error responses use the shared `code` extension from the global
 * exception handler; the application surfaces a generic message to the user and keeps
 * the raw response available for diagnostics.
 */

const API_BASE = "/api/v1";

export interface ProblemDetails {
  status: number;
  title: string;
  detail?: string;
  type?: string;
  instance?: string;
  code?: string;
  traceId?: string;
}

export class KairionError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;
  constructor(problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Kairion request failed (${problem.status}).`);
    this.status = problem.status;
    this.problem = problem;
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
      ...(init.headers ?? {}),
    },
    ...init,
  });

  if (!response.ok) {
    let problem: ProblemDetails;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      problem = {
        status: response.status,
        title: response.statusText,
        code: "unknown_error",
      };
    }
    throw new KairionError(problem);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export type BriefKind = "market" | "category" | "competitor" | "question";

export interface SourceProviderConfig {
  providerId: string;
  enabled: boolean;
  maxQueries: number;
  maxResultsPerQuery: number;
  endpoint?: string | null;
  credentialRef?: string | null;
}

export interface ResearchProject {
  id: string;
  title: string;
  briefKind: BriefKind;
  briefText: string;
  topics: string[];
  includedCompetitors: string[];
  enabledSourceProviderIds: string[];
  providerConfigs: SourceProviderConfig[];
  queryStrategy: string | null;
  windowStartUtc: string | null;
  windowEndUtc: string | null;
  state: string;
  createdUtc: string;
  updatedUtc: string;
  archivedUtc: string | null;
}

export interface CreateResearchProjectRequest {
  title: string;
  briefKind: BriefKind;
  briefText: string;
  topics?: string[];
  includedCompetitors?: string[];
  enabledSourceProviderIds?: string[];
  providerConfigs?: SourceProviderConfig[];
  queryStrategy?: string | null;
  windowStartUtc?: string | null;
  windowEndUtc?: string | null;
}

export interface SourceItem {
  id: string;
  projectId: string;
  providerId: string;
  externalId: string;
  canonicalUrl: string;
  title: string | null;
  excerpt: string | null;
  publishedUtc: string | null;
  observedUtc: string;
  effectiveDateUtc: string;
  providerStatus: string;
  duplicateOfId: string | null;
}

export interface ImportCandidateRequest {
  providerId: string;
  externalId: string;
  canonicalUrl: string;
  title?: string | null;
  excerpt?: string | null;
  publishedUtc?: string | null;
  observedUtc?: string;
  provenanceJson?: string;
}

export interface SourceQuery {
  query: string;
  topics: string[];
  includedCompetitors: string[];
  fromUtc?: string | null;
  toUtc?: string | null;
  limit?: number;
}

export interface TrendResponse {
  projectId: string;
  window: string;
  asOfUtc: string;
  windowStartUtc: string;
  windowEndUtc: string;
  currentCount: number;
  previousCount: number;
  percentGrowth: number | null;
  newSignal: boolean;
  label: string;
  clusters: ClusterTrend[];
}

export interface ClusterTrend {
  clusterId: string;
  label: string;
  currentCount: number;
  previousCount: number;
  percentGrowth: number | null;
  newSignal: boolean;
  labelTrend: string;
}

export interface ClusterResponse {
  id: string;
  projectId: string;
  label: string;
  category: string;
  summary: string;
  version: number;
  reviewStateVersion: number;
  evidenceCount: number;
  averageConfidence: number;
  createdUtc: string;
  updatedUtc: string;
}

export interface ClusterEvidenceResponse {
  clusterId: string;
  label: string;
  category: string;
  summary: string;
  reviewStateVersion: number;
  items: ClusterEvidenceItem[];
}

export interface ClusterEvidenceItem {
  sourceItemId: string;
  canonicalUrl: string;
  title: string | null;
  excerpt: string | null;
  publishedUtc: string | null;
  observedUtc: string;
  confidence: number;
  providerId: string;
  assignmentOrigin: string;
  assignedAtUtc: string;
}

export interface HumanRevisionResponse {
  id: string;
  action: string;
  clusterId: string | null;
  sourceItemId: string | null;
  payloadJson: string;
  createdUtc: string;
}

export interface OpportunitySignalResponse {
  id: string;
  clusterId: string;
  evidenceVolume: number;
  percentGrowth: number | null;
  newSignal: boolean;
  currentAlternatives: string[];
  workarounds: string[];
  confidence: number;
  aiExplanation: string | null;
  trendLabel: string;
  window: string;
  asOfUtc: string;
  windowStartUtc: string;
  windowEndUtc: string;
  generatedUtc: string;
  disclosure: string;
  representativeSourceItemIds: string[];
}

export interface SourceProviderInfo {
  providerId: string;
  displayName: string;
  requiresCredentials: boolean;
  available?: boolean;
}

export interface SourceIngestionRun {
  id: string;
  projectId: string;
  runId: string;
  providerId: string;
  status: string;
  candidateCount: number;
  diagnosticCode: string;
  retrievedAtUtc: string;
  retryAfterUtc: string | null;
}

export interface Competitor {
  id: string;
  projectId: string;
  name: string;
  createdUtc: string;
}

export interface CompetitorGapEvidenceRef {
  sourceItemId: string;
  canonicalUrl: string;
  title: string | null;
}

export interface CompetitorGapCell {
  competitorId: string | null;
  competitorName: string;
  clusterId: string;
  clusterLabel: string;
  evidenceCount: number;
  sourceCount: number;
  firstObservedUtc: string | null;
  lastObservedUtc: string | null;
  previousCount: number;
  delta: number | null;
  percentChange: number | null;
  classification: string;
  limitedEvidence: boolean;
  confidenceMean: number;
  confidenceSampleCount: number;
  representativeEvidence: CompetitorGapEvidenceRef[];
  stale: boolean;
}

export interface CompetitorGapResponse {
  projectId: string;
  window: string;
  asOfUtc: string;
  windowStartUtc: string;
  windowEndUtc: string;
  previousWindowStartUtc: string;
  previousWindowEndUtc: string;
  coverage: {
    totalEvidenceInWindow: number;
    mappedEvidenceInWindow: number;
    unmappedEvidenceInWindow: number;
    competitorCount: number;
    clusterCount: number;
    staleEvidenceCount: number;
  };
  competitors: Competitor[];
  cells: CompetitorGapCell[];
}

export interface AiProviderInfo {
  providerId: string;
  displayName: string;
  requiresCredentials: boolean;
}

export const api = {
  // Research projects (R1)
  listProjects: () => request<ResearchProject[]>(`/research-projects`),
  getProject: (id: string) => request<ResearchProject>(`/research-projects/${id}`),
  createProject: (body: CreateResearchProjectRequest) =>
    request<ResearchProject>(`/research-projects`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  updateProject: (id: string, body: CreateResearchProjectRequest) =>
    request<ResearchProject>(`/research-projects/${id}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  archiveProject: (id: string) =>
    request<void>(`/research-projects/${id}/archive`, { method: "POST" }),
  restoreProject: (id: string) =>
    request<void>(`/research-projects/${id}/restore`, { method: "POST" }),

  // Provider registry
  listSourceProviders: () => request<SourceProviderInfo[]>(`/providers/source`),
  listAiProviders: () => request<AiProviderInfo[]>(`/providers/ai`),

  // Candidate intake (R2)
  listCandidates: (projectId: string) =>
    request<SourceItem[]>(`/research-projects/${projectId}/candidates`),
  importCandidate: (projectId: string, body: ImportCandidateRequest) =>
    request<SourceItem>(`/research-projects/${projectId}/candidates/import`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  searchCandidates: (projectId: string, body: SourceQuery) =>
    request<SourceItem[]>(`/research-projects/${projectId}/candidates/search`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  collectCandidates: (projectId: string, body: { text: string; topics: string[]; maxResults: number }) =>
    request<SourceItem[]>(`/research-projects/${projectId}/candidates/collect`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  listSourceRuns: (projectId: string) =>
    request<SourceIngestionRun[]>(`/research-projects/${projectId}/source-runs`),

  // AI analysis (R3)
  screenCandidate: (projectId: string, sourceItemId: string, aiProviderId: string) =>
    request<unknown>(
      `/research-projects/${projectId}/candidates/${sourceItemId}/screening?aiProviderId=${encodeURIComponent(aiProviderId)}`,
      { method: "POST" }
    ),
  analyzeCandidate: (projectId: string, sourceItemId: string, aiProviderId: string) =>
    request<unknown>(
      `/research-projects/${projectId}/candidates/${sourceItemId}/analysis?aiProviderId=${encodeURIComponent(aiProviderId)}`,
      { method: "POST" }
    ),

  // Clusters (R4)
  listClusters: (projectId: string) =>
    request<ClusterResponse[]>(`/research-projects/${projectId}/clusters`),
  getClusterEvidence: (clusterId: string) =>
    request<ClusterEvidenceResponse>(`/clusters/${clusterId}`),

  // Trends (R5)
  projectTrends: (projectId: string, window: string) =>
    request<TrendResponse>(`/research-projects/${projectId}/trends?window=${window}`),

  // Opportunity (R6)
  opportunityForCluster: (projectId: string, clusterId: string, window = "30d") =>
    request<OpportunitySignalResponse>(
      `/research-projects/${projectId}/opportunity-signals/${clusterId}?window=${window}`
    ),

  // Competitor gaps (read-only comparison matrix)
  listCompetitors: (projectId: string) =>
    request<Competitor[]>(`/research-projects/${projectId}/competitors`),
  assignCompetitor: (projectId: string, sourceItemId: string, competitorId: string) =>
    request<Competitor>(
      `/research-projects/${projectId}/candidates/${sourceItemId}/competitor-assignments`,
      { method: "POST", body: JSON.stringify({ competitorId }) }
    ),
  unassignCompetitor: (projectId: string, sourceItemId: string, competitorId: string) =>
    request<void>(
      `/research-projects/${projectId}/candidates/${sourceItemId}/competitor-assignments/${competitorId}`,
      { method: "DELETE" }
    ),
  competitorGaps: (projectId: string, window: string, competitorIds: string[] = []) => {
    const filter = competitorIds.map((id) => `&competitorId=${encodeURIComponent(id)}`).join("");
    return request<CompetitorGapResponse>(
      `/research-projects/${projectId}/competitor-gaps?window=${window}${filter}`
    );
  },
};

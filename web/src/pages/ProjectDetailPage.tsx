import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FormEvent, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  api,
  ClusterResponse,
  KairionError,
  ResearchProject,
  SourceItem,
  TrendResponse,
} from "../api";

type TrendWindow = "7d" | "30d" | "90d";

/**
 * R1 / R2 / R4 / R5 / R6 — Project detail page. Combines:
 *   - the research project brief and source configuration,
 *   - manual URL intake and provider search (R2),
 *   - per-window trends (R5) with the deterministic label,
 *   - cluster list with the source-evidence count and average confidence (R4),
 *   - a deep-link to the per-cluster evidence board and opportunity signal.
 */
export function ProjectDetailPage() {
  const { projectId } = useParams<{ projectId: string }>();
  if (!projectId) return <div className="empty">Missing project id.</div>;

  const queryClient = useQueryClient();
  const project = useQuery({
    queryKey: ["project", projectId],
    queryFn: () => api.getProject(projectId),
  });
  const candidates = useQuery({
    queryKey: ["candidates", projectId],
    queryFn: () => api.listCandidates(projectId),
  });
  const clusters = useQuery({
    queryKey: ["clusters", projectId],
    queryFn: () => api.listClusters(projectId),
  });
  const aiProviders = useQuery({
    queryKey: ["ai-providers"],
    queryFn: () => api.listAiProviders(),
  });
  const [window, setWindow] = useState<TrendWindow>("30d");
  const trends = useQuery<TrendResponse>({
    queryKey: ["trends", projectId, window],
    queryFn: () => api.projectTrends(projectId, window),
  });

  const [importUrl, setImportUrl] = useState("");
  const [importTitle, setImportTitle] = useState("");
  const importCandidate = useMutation({
    mutationFn: () =>
      api.importCandidate(projectId, {
        providerId: "manual",
        externalId: crypto.randomUUID(),
        canonicalUrl: importUrl.trim(),
        title: importTitle.trim() || null,
      }),
    onSuccess: () => {
      setImportUrl("");
      setImportTitle("");
      queryClient.invalidateQueries({ queryKey: ["candidates", projectId] });
    },
  });

  const onImport = (e: FormEvent) => {
    e.preventDefault();
    if (!importUrl.trim()) return;
    importCandidate.mutate();
  };

  const screenOne = useMutation({
    mutationFn: ({ sourceItemId, aiProviderId }: { sourceItemId: string; aiProviderId: string }) =>
      api.screenCandidate(projectId, sourceItemId, aiProviderId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["candidates", projectId] });
    },
  });
  const analyzeOne = useMutation({
    mutationFn: ({ sourceItemId, aiProviderId }: { sourceItemId: string; aiProviderId: string }) =>
      api.analyzeCandidate(projectId, sourceItemId, aiProviderId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["candidates", projectId] });
    },
  });

  if (project.isLoading) return <div className="empty">Loading project…</div>;
  if (project.error) return <div className="error">Failed to load project.</div>;
  if (!project.data) return <div className="empty">Project not found.</div>;

  const p: ResearchProject = project.data;

  return (
    <>
      <div className="card">
        <div className="row" style={{ justifyContent: "space-between" }}>
          <div>
            <h2 style={{ margin: 0 }}>{p.title}</h2>
            <div className="muted" style={{ marginTop: 4 }}>
              <span className="pill">{p.briefKind}</span> · created{" "}
              {new Date(p.createdUtc).toISOString().slice(0, 10)} · UTC
            </div>
          </div>
          <Link to="/projects">← All projects</Link>
        </div>
        <p style={{ marginTop: 12 }}>{p.briefText}</p>
        <div className="row tight" style={{ marginTop: 6, flexWrap: "wrap" }}>
          {p.topics.map((t) => (
            <span key={t} className="pill">
              {t}
            </span>
          ))}
          {p.includedCompetitors.map((c) => (
            <span key={c} className="pill" style={{ background: "#fde7e5" }}>
              {c}
            </span>
          ))}
        </div>
        <div className="muted" style={{ marginTop: 8 }}>
          Source providers: {p.enabledSourceProviderIds.join(", ") || "(none — manual only)"}
        </div>
      </div>

      <div className="card">
        <div className="row" style={{ justifyContent: "space-between" }}>
          <h2 style={{ margin: 0 }}>Trends</h2>
          <div className="row tight">
            {(["7d", "30d", "90d"] as const).map((w) => (
              <button
                key={w}
                type="button"
                className={window === w ? "primary" : ""}
                onClick={() => setWindow(w)}
              >
                {w}
              </button>
            ))}
          </div>
        </div>
        {trends.isLoading ? (
          <div className="empty">Computing…</div>
        ) : trends.error ? (
          <div className="error">Failed to load trends.</div>
        ) : trends.data ? (
          <>
            <div className="kpi-grid" style={{ marginTop: 8 }}>
              <div className="kpi">
                <div className="label">Current window</div>
                <div className="value">{trends.data.currentCount}</div>
              </div>
              <div className="kpi">
                <div className="label">Previous window</div>
                <div className="value">{trends.data.previousCount}</div>
              </div>
              <div className="kpi">
                <div className="label">Percent growth</div>
                <div className="value">
                  {trends.data.percentGrowth === null
                    ? trends.data.newSignal
                      ? "new"
                      : "—"
                    : `${(trends.data.percentGrowth * 100).toFixed(1)}%`}
                </div>
              </div>
              <div className="kpi">
                <div className="label">Label</div>
                <div className="value">
                  <span
                    className={
                      trends.data.label === "emerging"
                        ? "pill success"
                        : trends.data.label === "declining"
                        ? "pill fail"
                        : "pill"
                    }
                  >
                    {trends.data.label}
                  </span>
                </div>
              </div>
            </div>
            <div className="muted" style={{ marginTop: 8 }}>
              Window {trends.data.windowStartUtc.slice(0, 10)} →{" "}
              {trends.data.windowEndUtc.slice(0, 10)} · as-of {trends.data.asOfUtc.slice(0, 19)}Z
            </div>
          </>
        ) : null}
      </div>

      <div className="card">
        <h2>Candidate evidence</h2>
        <p className="muted">
          Manually paste a canonical URL to ingest a source candidate. The same
          identity is reused on re-import (R2). The screening and analysis steps
          call the AI provider and are typed + schema-validated (R3).
        </p>
        <form onSubmit={onImport}>
          <div style={{ display: "grid", gridTemplateColumns: "3fr 2fr auto", gap: 8 }}>
            <input
              value={importUrl}
              onChange={(e) => setImportUrl(e.target.value)}
              placeholder="https://example.com/thread/123"
              required
            />
            <input
              value={importTitle}
              onChange={(e) => setImportTitle(e.target.value)}
              placeholder="optional title"
            />
            <button type="submit" className="primary" disabled={importCandidate.isPending}>
              {importCandidate.isPending ? "Importing…" : "Import"}
            </button>
          </div>
          {importCandidate.error ? (
            <div className="error" style={{ marginTop: 8 }}>
              {importCandidate.error instanceof KairionError
                ? `${importCandidate.error.problem.code ?? "error"}: ${
                    importCandidate.error.problem.detail ?? importCandidate.error.message
                  }`
                : String(importCandidate.error)}
            </div>
          ) : null}
        </form>

        {candidates.isLoading ? (
          <div className="empty">Loading candidates…</div>
        ) : candidates.data && candidates.data.length > 0 ? (
          <table style={{ marginTop: 12 }}>
            <thead>
              <tr>
                <th>Title</th>
                <th>Source</th>
                <th>Observed (UTC)</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {candidates.data.map((s: SourceItem) => (
                <tr key={s.id}>
                  <td>
                    <a href={s.canonicalUrl} target="_blank" rel="noreferrer">
                      {s.title ?? s.canonicalUrl}
                    </a>
                  </td>
                  <td>
                    <span className="pill">{s.providerId}</span>
                  </td>
                  <td>{s.observedUtc.slice(0, 19).replace("T", " ")}</td>
                  <td>
                    <span
                      className={
                        s.providerStatus === "Available"
                          ? "pill success"
                          : "pill warn"
                      }
                    >
                      {s.providerStatus}
                    </span>
                  </td>
                  <td>
                    <div className="row tight">
                      {aiProviders.data && aiProviders.data[0] ? (
                        <>
                          <button
                            type="button"
                            onClick={() =>
                              screenOne.mutate({
                                sourceItemId: s.id,
                                aiProviderId: aiProviders.data![0].providerId,
                              })
                            }
                            disabled={screenOne.isPending}
                          >
                            Screen
                          </button>
                          <button
                            type="button"
                            onClick={() =>
                              analyzeOne.mutate({
                                sourceItemId: s.id,
                                aiProviderId: aiProviders.data![0].providerId,
                              })
                            }
                            disabled={analyzeOne.isPending}
                          >
                            Analyze
                          </button>
                        </>
                      ) : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <div className="empty" style={{ marginTop: 12 }}>
            No candidates yet.
          </div>
        )}
      </div>

      <div className="card">
        <h2>Pain clusters</h2>
        {clusters.isLoading ? (
          <div className="empty">Loading…</div>
        ) : clusters.data && clusters.data.length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>Label</th>
                <th>Category</th>
                <th>Evidence</th>
                <th>Avg confidence</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {clusters.data.map((c: ClusterResponse) => (
                <tr key={c.id}>
                  <td>
                    <Link to={`/clusters/${c.id}`}>{c.label}</Link>
                  </td>
                  <td>
                    <span className="pill">{c.category}</span>
                  </td>
                  <td>{c.evidenceCount}</td>
                  <td>
                    {c.averageConfidence === null
                      ? "—"
                      : c.averageConfidence.toFixed(2)}
                  </td>
                  <td>
                    <Link to={`/clusters/${c.id}`}>Open →</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <div className="empty">
            No clusters yet. Once a candidate passes screening + analysis it becomes
            eligible for clustering.
          </div>
        )}
      </div>
    </>
  );
}

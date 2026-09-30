import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FormEvent, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  api,
  ClusterResponse,
  KairionError,
  ResearchProject,
  SourceItem,
  SourceProviderConfig,
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
  const sourceProviders = useQuery({
    queryKey: ["source-providers"],
    queryFn: () => api.listSourceProviders(),
  });
  const sourceRuns = useQuery({
    queryKey: ["source-runs", projectId],
    queryFn: () => api.listSourceRuns(projectId),
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

  const saveProviders = useMutation({
    mutationFn: (configs: SourceProviderConfig[]) => {
      const current = project.data;
      if (!current) throw new Error("Project not loaded.");
      return api.updateProject(projectId, {
        title: current.title,
        briefKind: current.briefKind,
        briefText: current.briefText,
        topics: current.topics,
        includedCompetitors: current.includedCompetitors,
        providerConfigs: configs,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["project", projectId] });
    },
  });

  const collectAll = useMutation({
    mutationFn: () => {
      const current = project.data;
      return api.collectCandidates(projectId, {
        text: current?.briefText ?? "",
        topics: current?.topics ?? [],
        maxResults: 25,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["candidates", projectId] });
      queryClient.invalidateQueries({ queryKey: ["source-runs", projectId] });
    },
  });

  const toggleProvider = (providerId: string, configs: SourceProviderConfig[]) => {
    const existing = configs.find((c) => c.providerId === providerId);
    const next: SourceProviderConfig[] = existing
      ? configs.map((c) =>
          c.providerId === providerId ? { ...c, enabled: !c.enabled } : c
        )
      : [...configs, { providerId, enabled: true, maxQueries: 5, maxResultsPerQuery: 50 }];
    saveProviders.mutate(next);
  };

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
          <h2 style={{ margin: 0 }}>Source providers</h2>
          <button
            type="button"
            className="primary"
            onClick={() => collectAll.mutate()}
            disabled={collectAll.isPending}
          >
            {collectAll.isPending ? "Collecting…" : "Collect from enabled"}
          </button>
        </div>
        <p className="muted">
          Providers are disabled until explicitly enabled. Limits are bounded to
          1–5 queries per run and 1–50 results per query. Secrets stay in
          deployment configuration, never in project records.
        </p>
        {sourceProviders.isLoading ? (
          <div className="empty">Loading providers…</div>
        ) : sourceProviders.data ? (
          <table>
            <thead>
              <tr>
                <th>Provider</th>
                <th>Health</th>
                <th>Limits</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {sourceProviders.data.map((sp) => {
                const cfg = (p.providerConfigs ?? []).find(
                  (c) => c.providerId === sp.providerId
                );
                const enabled = cfg?.enabled ?? false;
                return (
                  <tr key={sp.providerId}>
                    <td>
                      <span className="pill">{sp.providerId}</span>{" "}
                      <span className="muted">{sp.displayName}</span>
                      {sp.requiresCredentials ? (
                        <span className="pill warn" style={{ marginLeft: 6 }}>
                          BYOK
                        </span>
                      ) : null}
                    </td>
                    <td>
                      <span
                        className={
                          sp.available ? "pill success" : "pill warn"
                        }
                      >
                        {sp.available ? "available" : "unavailable"}
                      </span>{" "}
                      <span
                        className={enabled ? "pill success" : "pill"}
                      >
                        {enabled ? "enabled" : "disabled"}
                      </span>
                    </td>
                    <td className="muted">
                      {cfg
                        ? `${cfg.maxQueries} q/run · ${cfg.maxResultsPerQuery} r/q`
                        : "defaults (5 q/run · 50 r/q)"}
                      {cfg?.endpoint ? (
                        <div style={{ fontSize: 12 }}>{cfg.endpoint}</div>
                      ) : null}
                    </td>
                    <td>
                      <button
                        type="button"
                        onClick={() =>
                          toggleProvider(sp.providerId, p.providerConfigs ?? [])
                        }
                        disabled={saveProviders.isPending}
                      >
                        {enabled ? "Disable" : "Enable"}
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        ) : null}
        {saveProviders.error ? (
          <div className="error" style={{ marginTop: 8 }}>
            {saveProviders.error instanceof KairionError
              ? `${saveProviders.error.problem.code ?? "error"}: ${
                  saveProviders.error.problem.detail ?? saveProviders.error.message
                }`
              : String(saveProviders.error)}
          </div>
        ) : null}
        <h3 style={{ marginTop: 16 }}>Recent runs</h3>
        {sourceRuns.isLoading ? (
          <div className="empty">Loading runs…</div>
        ) : sourceRuns.data && sourceRuns.data.length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>Provider</th>
                <th>Status</th>
                <th>Candidates</th>
                <th>Diagnostic</th>
                <th>Retrieved (UTC)</th>
              </tr>
            </thead>
            <tbody>
              {sourceRuns.data.slice(0, 10).map((r) => (
                <tr key={r.id}>
                  <td>
                    <span className="pill">{r.providerId}</span>
                  </td>
                  <td>
                    <span
                      className={
                        r.status === "Complete"
                          ? "pill success"
                          : r.status === "Partial"
                          ? "pill warn"
                          : "pill fail"
                      }
                    >
                      {r.status}
                    </span>
                  </td>
                  <td>{r.candidateCount}</td>
                  <td className="muted">{r.diagnosticCode}</td>
                  <td>{r.retrievedAtUtc.slice(0, 19).replace("T", " ")}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <div className="empty">No ingestion runs yet.</div>
        )}
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

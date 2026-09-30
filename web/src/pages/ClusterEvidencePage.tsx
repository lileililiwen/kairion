import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import {
  api,
  ClusterEvidenceItem,
  ClusterEvidenceResponse,
  OpportunitySignalResponse,
} from "../api";

/**
 * R4 / R6 — Cluster evidence board. Shows every source item assigned to the
 * cluster with its observed timestamp and provider, plus the Opportunity
 * Signal summary. The opportunity disclosure is always rendered to make sure
 * the user sees that the signal is evidence, not business validation.
 */
export function ClusterEvidencePage() {
  const { clusterId } = useParams<{ clusterId: string }>();
  if (!clusterId) return <div className="empty">Missing cluster id.</div>;

  // Look up the cluster via the list to discover its projectId (used for the
  // opportunity signal endpoint which is nested under the project).
  const cluster = useQuery({
    queryKey: ["cluster-evidence", clusterId],
    queryFn: () => api.getClusterEvidence(clusterId),
  });
  const projectClusters = useQuery({
    queryKey: ["clusters-for-project", cluster.data?.label ?? ""],
    queryFn: async () => {
      const all = await api.listProjects();
      for (const p of all) {
        const list = await api.listClusters(p.id).catch(() => []);
        const found = list.find((c) => c.id === clusterId);
        if (found) return { projectId: p.id, cluster: found };
      }
      return null;
    },
    enabled: !!cluster.data,
  });
  const opportunity = useQuery<OpportunitySignalResponse>({
    queryKey: ["opportunity", clusterId, projectClusters.data?.projectId],
    queryFn: () =>
      api.opportunityForCluster(projectClusters.data!.projectId, clusterId),
    enabled: !!projectClusters.data?.projectId,
  });

  if (cluster.isLoading) return <div className="empty">Loading…</div>;
  if (cluster.error || !cluster.data)
    return <div className="error">Failed to load cluster.</div>;
  const c: ClusterEvidenceResponse = cluster.data;

  return (
    <>
      <div className="card">
        <div className="row" style={{ justifyContent: "space-between" }}>
          <div>
            <h2 style={{ margin: 0 }}>{c.label}</h2>
            <div className="muted" style={{ marginTop: 4 }}>
              <span className="pill">{c.category}</span> · review v
              {c.reviewStateVersion}
            </div>
          </div>
          {projectClusters.data?.projectId ? (
            <Link to={`/projects/${projectClusters.data.projectId}`}>← Project</Link>
          ) : null}
        </div>
        <p style={{ marginTop: 12 }}>{c.summary}</p>
        <div className="kpi-grid" style={{ marginTop: 8 }}>
          <div className="kpi">
            <div className="label">Evidence</div>
            <div className="value">{c.items.length}</div>
          </div>
          <div className="kpi">
            <div className="label">Avg confidence</div>
            <div className="value">
              {c.items.length === 0
                ? "—"
                : (
                    c.items.reduce((acc, i) => acc + (i.confidence || 0), 0) /
                    c.items.length
                  ).toFixed(2)}
            </div>
          </div>
        </div>
      </div>

      {opportunity.data ? (
        <div className="card">
          <h2>Opportunity signal</h2>
          <div className="disclosure" role="note">
            {opportunity.data.disclosure}
          </div>
          {opportunity.data.evidenceVolume === 0 ? (
            <div className="empty">No retained evidence — no positive signal.</div>
          ) : (
            <>
              {opportunity.data.aiExplanation ? (
                <p>{opportunity.data.aiExplanation}</p>
              ) : null}
              <div className="kpi-grid">
                <div className="kpi">
                  <div className="label">Window</div>
                  <div className="value">{opportunity.data.window}</div>
                </div>
                <div className="kpi">
                  <div className="label">Evidence</div>
                  <div className="value">{opportunity.data.evidenceVolume}</div>
                </div>
                <div className="kpi">
                  <div className="label">Growth</div>
                  <div className="value">
                    {opportunity.data.percentGrowth === null
                      ? opportunity.data.newSignal
                        ? "new"
                        : "—"
                      : `${(opportunity.data.percentGrowth * 100).toFixed(1)}%`}
                  </div>
                </div>
                <div className="kpi">
                  <div className="label">Label</div>
                  <div className="value">
                    <span className="pill">{opportunity.data.trendLabel}</span>
                  </div>
                </div>
                <div className="kpi">
                  <div className="label">Confidence</div>
                  <div className="value">
                    {opportunity.data.confidence.toFixed(2)}
                  </div>
                </div>
              </div>
              {opportunity.data.currentAlternatives.length > 0 ? (
                <div style={{ marginTop: 12 }}>
                  <h3>Current alternatives</h3>
                  <ul>
                    {opportunity.data.currentAlternatives.map((a, i) => (
                      <li key={i}>{a}</li>
                    ))}
                  </ul>
                </div>
              ) : null}
              {opportunity.data.workarounds.length > 0 ? (
                <div style={{ marginTop: 12 }}>
                  <h3>Workarounds</h3>
                  <ul>
                    {opportunity.data.workarounds.map((w, i) => (
                      <li key={i}>{w}</li>
                    ))}
                  </ul>
                </div>
              ) : null}
              {opportunity.data.representativeSourceItemIds.length > 0 ? (
                <div className="muted" style={{ marginTop: 12 }}>
                  Representative sources:{" "}
                  {opportunity.data.representativeSourceItemIds.join(", ")}
                </div>
              ) : null}
            </>
          )}
        </div>
      ) : opportunity.isLoading ? (
        <div className="card">
          <h2>Opportunity signal</h2>
          <div className="empty">Computing…</div>
        </div>
      ) : null}

      <div className="card">
        <h2>Evidence board</h2>
        {c.items.length === 0 ? (
          <div className="empty">No source items linked to this cluster yet.</div>
        ) : (
          c.items.map((s: ClusterEvidenceItem) => (
            <div className="evidence" key={s.sourceItemId}>
              <div className="row" style={{ justifyContent: "space-between" }}>
                <a href={s.canonicalUrl} target="_blank" rel="noreferrer">
                  {s.title ?? s.canonicalUrl}
                </a>
                <span className="meta">{s.providerId}</span>
              </div>
              {s.excerpt ? <p style={{ marginTop: 6 }}>{s.excerpt}</p> : null}
              <div className="meta">
                Observed {s.observedUtc.slice(0, 19).replace("T", " ")}Z · origin{" "}
                {s.assignmentOrigin} · confidence {s.confidence.toFixed(2)}
              </div>
            </div>
          ))
        )}
      </div>
    </>
  );
}

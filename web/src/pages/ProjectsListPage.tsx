import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FormEvent, useState } from "react";
import { Link } from "react-router-dom";
import { api, BriefKind, KairionError, ResearchProject } from "../api";

/**
 * R1 — Research projects list and create flow. The create form validates
 * locally before the request so the user sees immediate feedback; the API
 * still re-validates and returns a 400 with a stable `code` if anything slips
 * through (e.g. backend adds a stricter rule).
 */
export function ProjectsListPage() {
  const queryClient = useQueryClient();
  const projects = useQuery({
    queryKey: ["projects"],
    queryFn: () => api.listProjects(),
  });

  const [title, setTitle] = useState("");
  const [briefKind, setBriefKind] = useState<BriefKind>("market");
  const [briefText, setBriefText] = useState("");
  const [topics, setTopics] = useState("");
  const [competitors, setCompetitors] = useState("");

  const create = useMutation({
    mutationFn: () =>
      api.createProject({
        title: title.trim(),
        briefKind,
        briefText: briefText.trim(),
        topics: topics
          .split(",")
          .map((s) => s.trim())
          .filter(Boolean),
        includedCompetitors: competitors
          .split(",")
          .map((s) => s.trim())
          .filter(Boolean),
        enabledSourceProviderIds: ["manual"],
      }),
    onSuccess: () => {
      setTitle("");
      setBriefText("");
      setTopics("");
      setCompetitors("");
      queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !briefText.trim()) return;
    create.mutate();
  };

  return (
    <>
      <div className="card">
        <h2>Start a new research project</h2>
        <p className="muted">
          A research project owns one brief, the source configuration, the candidate
          evidence, the clusters, and the resulting opportunity signals. The brief
          is the only field the AI summarisation pipeline reads.
        </p>
        <form onSubmit={onSubmit}>
          <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 12 }}>
            <div>
              <label htmlFor="title">Project title</label>
              <input
                id="title"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="e.g. Recurring billing complaints in SMB SaaS"
                required
                style={{ width: "100%" }}
              />
            </div>
            <div>
              <label htmlFor="briefKind">Brief kind</label>
              <select
                id="briefKind"
                value={briefKind}
                onChange={(e) => setBriefKind(e.target.value as BriefKind)}
                style={{ width: "100%" }}
              >
                <option value="market">Market</option>
                <option value="category">Category</option>
                <option value="competitor">Competitor</option>
                <option value="question">Question</option>
              </select>
            </div>
          </div>
          <div style={{ marginTop: 12 }}>
            <label htmlFor="briefText">Brief</label>
            <textarea
              id="briefText"
              value={briefText}
              onChange={(e) => setBriefText(e.target.value)}
              rows={3}
              required
              style={{ width: "100%" }}
              placeholder="What recurring problem are you trying to understand? Who is the audience?"
            />
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginTop: 12 }}>
            <div>
              <label htmlFor="topics">Topics (comma-separated)</label>
              <input
                id="topics"
                value={topics}
                onChange={(e) => setTopics(e.target.value)}
                placeholder="billing, churn, SaaS"
                style={{ width: "100%" }}
              />
            </div>
            <div>
              <label htmlFor="competitors">Competitors (comma-separated)</label>
              <input
                id="competitors"
                value={competitors}
                onChange={(e) => setCompetitors(e.target.value)}
                placeholder="Stripe, Chargebee"
                style={{ width: "100%" }}
              />
            </div>
          </div>
          {create.error ? (
            <div className="error" style={{ marginTop: 12 }}>
              {create.error instanceof KairionError
                ? `${create.error.problem.code ?? "error"}: ${create.error.problem.detail ?? create.error.message}`
                : String(create.error)}
            </div>
          ) : null}
          <div className="row" style={{ marginTop: 12, justifyContent: "flex-end" }}>
            <button type="submit" className="primary" disabled={create.isPending}>
              {create.isPending ? "Creating…" : "Create project"}
            </button>
          </div>
        </form>
      </div>

      <div className="card">
        <h2>Active research projects</h2>
        {projects.isLoading ? (
          <div className="empty">Loading…</div>
        ) : projects.error ? (
          <div className="error">
            Failed to load projects: {String(projects.error)}
          </div>
        ) : !projects.data || projects.data.length === 0 ? (
          <div className="empty">No projects yet. Create one above to get started.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Title</th>
                <th>Brief</th>
                <th>Topics</th>
                <th>Updated (UTC)</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {projects.data.map((p: ResearchProject) => (
                <tr key={p.id}>
                  <td>
                    <Link to={`/projects/${p.id}`}>{p.title}</Link>
                  </td>
                  <td>
                    <span className="pill">{p.briefKind}</span>
                  </td>
                  <td>
                    {p.topics.length === 0
                      ? "—"
                      : p.topics.map((t) => (
                          <span key={t} className="pill" style={{ marginRight: 4 }}>
                            {t}
                          </span>
                        ))}
                  </td>
                  <td>{new Date(p.updatedUtc).toISOString().replace("T", " ").slice(0, 19)}</td>
                  <td>
                    <Link to={`/projects/${p.id}`}>Open →</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}

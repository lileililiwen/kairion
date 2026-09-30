import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api, ResearchProject } from "../api";

/**
 * R6 — Cross-project opportunity signal list. The MVP keeps this lightweight:
 * it lists the active projects and links to the per-project evidence view so
 * the user can drill into each cluster's signal.
 */
export function OpportunitiesPage() {
  const clusters = useQuery({
    queryKey: ["all-clusters"],
    queryFn: async () => {
      const allProjects = (await api.listProjects()) as ResearchProject[];
      const results = await Promise.all(
        allProjects.map(async (p) => ({
          project: p,
          clusters: await api.listClusters(p.id).catch(() => []),
        }))
      );
      return results;
    },
  });

  return (
    <>
      <div className="card">
        <h2>Opportunity signals</h2>
        <div className="disclosure" role="note">
          Every signal below is an evidence signal — a summary of the source
          items the system has retained. It is not business validation.
        </div>
        {clusters.isLoading ? (
          <div className="empty">Loading…</div>
        ) : clusters.data && clusters.data.length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>Cluster</th>
                <th>Project</th>
                <th>Category</th>
                <th>Evidence</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {clusters.data.flatMap(({ project, clusters }) =>
                clusters.map((c) => (
                  <tr key={c.id}>
                    <td>
                      <Link to={`/clusters/${c.id}`}>{c.label}</Link>
                    </td>
                    <td>
                      <Link to={`/projects/${project.id}`}>{project.title}</Link>
                    </td>
                    <td>
                      <span className="pill">{c.category}</span>
                    </td>
                    <td>{c.evidenceCount}</td>
                    <td>
                      <Link to={`/clusters/${c.id}`}>Open →</Link>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        ) : (
          <div className="empty">
            No projects yet. Create one in <Link to="/projects">Projects</Link>.
          </div>
        )}
      </div>
    </>
  );
}

import { Link, NavLink, Navigate, Route, Routes } from "react-router-dom";
import { ProjectsListPage } from "./pages/ProjectsListPage";
import { ProjectDetailPage } from "./pages/ProjectDetailPage";
import { ClusterEvidencePage } from "./pages/ClusterEvidencePage";
import { OpportunitiesPage } from "./pages/OpportunitiesPage";

/**
 * Top-level layout. The header advertises the product and exposes the primary
 * navigation. The MVP keeps the routes flat: projects list → project detail →
 * cluster evidence / opportunity signal.
 */
export function App() {
  return (
    <div className="layout">
      <header className="header">
        <h1>Kairion</h1>
        <span className="tag">evidence-grounded demand research</span>
        <nav style={{ marginLeft: "auto" }}>
          <NavLink to="/projects" className={({ isActive }) => (isActive ? "active" : "")}>
            Projects
          </NavLink>
          <NavLink to="/opportunities" className={({ isActive }) => (isActive ? "active" : "")}>
            Opportunity signals
          </NavLink>
        </nav>
      </header>
      <main className="main">
        <Routes>
          <Route path="/" element={<Navigate to="/projects" replace />} />
          <Route path="/projects" element={<ProjectsListPage />} />
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
          <Route path="/clusters/:clusterId" element={<ClusterEvidencePage />} />
          <Route path="/opportunities" element={<OpportunitiesPage />} />
          <Route
            path="*"
            element={
              <div className="empty">
                Page not found. <Link to="/projects">Back to projects.</Link>
              </div>
            }
          />
        </Routes>
      </main>
    </div>
  );
}

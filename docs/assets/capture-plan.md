# Documentation capture plan

- **Purpose:** capture the real Kairion research-project and evidence-board UI for README documentation.
- **Source:** local application routes `http://127.0.0.1:5288/` (API) and `http://127.0.0.1:5173/` (web, served by `vite preview`).
- **Command/settings:** `shot-scraper <url> --output docs/assets/<name>.jpg --width 1440 --height 900 --wait 5000 --quality 85`.
- **Artifacts:**
  - `docs/assets/README.jpg` — projects list page (`/projects`).
  - `docs/assets/project-detail.jpg` — project detail page (`/projects/{id}`) with trends, candidate evidence, and clusters.
  - `docs/assets/cluster-evidence.jpg` — cluster evidence board + opportunity signal (`/clusters/{id}`).
- **Revision/time:** captured on 2026-09-30 from the working-tree state of the `evidence-grounded-demand-research-mvp` OpenSpec change, against a local Postgres 16 instance seeded with one synthetic research project, two source candidates, and one pain cluster.
- **Privacy review:** all data is synthetic and was hand-typed for the screenshot — no real customer, no API keys, no credentials, no real discussion content. The candidate URLs point to `example.test` and `news.ycombinator.com/item?id=…` placeholders, the cluster label/category/summary are illustrative, and the project brief is generic. The web app reads BYOK secrets only from environment variables; no secret values are echoed in any UI.
- **Status:** CAPTURED. The MVP renders end-to-end against PostgreSQL 16 and the artifacts are checked in. The next capture after the next OpenSpec change should re-run the same commands against the updated UI.

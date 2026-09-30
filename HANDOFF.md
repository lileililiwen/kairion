current_spec: evidence-grounded-demand-research-mvp

# Handoff

## State

The `evidence-grounded-demand-research-mvp` OpenSpec change (R1–R6) is implemented, verified end-to-end against PostgreSQL 16, archived, and shipped on `main`. The .NET 10 API, EF Core persistence with an initial migration, the React/TypeScript web app, and the local Docker Compose stack all run and are covered by tests. No active OpenSpec change is committed; the follow-up planning packages exist only as uncommitted working-tree directories (see below).

## Completed change

`evidence-grounded-demand-research-mvp` → archived as `openspec/changes/archive/2026-09-30-evidence-grounded-demand-research-mvp/`, with the generated capability spec at `openspec/specs/demand-research/spec.md`. All R1–R6 requirements and their scenarios are satisfied against real code.

## Verification evidence

- `dotnet build Kairion.slnx` — 0 warnings, 0 errors.
- `dotnet test Kairion.slnx` — 41/41 unit tests and 26/26 integration tests pass.
- `dotnet format Kairion.slnx --verify-no-changes` — exit 0.
- `npm ci`, `npm run typecheck`, `npm run build`, `npm test` (in `web/`) — typecheck clean, production bundle builds, 1/1 smoke test passes.
- `openspec validate --all --strict --no-interactive` — demand-research spec and the two working-tree follow-up changes pass.
- `python3 scripts/verify_bootstrap.py` — repository metadata and OpenSpec state consistent (script updated to accept the post-archive state).
- Postgres end-to-end: applied `InitialSchema` to PostgreSQL 16, exercised project create, candidate import, duplicate re-import, trends (`newSignal: true`, `percentGrowth: null` on a zero baseline), screening, and cluster evidence reads against the real database.
- Privacy-reviewed screenshots captured from the running app and checked in: `docs/assets/README.jpg` (projects list), `docs/assets/project-detail.jpg` (brief, trends, candidate evidence, clusters), `docs/assets/cluster-evidence.jpg` (evidence board + opportunity disclosure). All data is synthetic; no keys, credentials, or personal data appear. `docs/assets/capture-plan.md` records the commands and review.
- Workspace Governance: registered `kairion` in `../workspace-governance/projects.json` (`dotnet-product`); the project-scoped check now reports zero kairion findings.

## Known blockers (outside this repository)

- The portfolio-wide workspace Gate still reports FAIL because of pre-existing issues in sibling projects (for example registered `jenkins-bootstrap` directory missing and unregistered `argoscope`), not because of kairion. The kairion-specific finding count is 0 after registration.

## Uncommitted working-tree state

`openspec/changes/additional-source-adapters/` and `openspec/changes/competitor-gap-analysis/` are complete follow-up planning packages and are intentionally left uncommitted so the completed change keeps its exact two-commit lifecycle. They are eligible to become the next active change.

## Next actions

1. Select `additional-source-adapters` as the next active change and commit its package.
2. Report the unrelated portfolio Gate failures to the workspace owner.
3. Implement the next package only after its own BFS/DFS/BFS cycle.

Each completed OpenSpec spec/change requires exactly two commits: first the implementation/tests/archive commit, then a HANDOFF.md-only pointer/evidence commit.

current_spec: competitor-gap-analysis

# Handoff

## State

The `competitor-gap-analysis` OpenSpec change (R1–R3 + matrix view) is implemented, verified against the full .NET + web suites, archived, and shipped on `main`. Project-scoped competitors now have stable owner-managed identity (`competitors` table, unique per project by normalized name, synced from `IncludedCompetitors` without deleting attributed rows); source evidence is attributed only through explicit `source_item_competitors` assignments (owner POST, idempotent; DELETE to remove); the read-only `GET /api/v1/research-projects/{id}/competitor-gaps` matrix derives distinct item/source counts, previous-window deltas, `Emerging|Stable|Declining|InsufficientData` classification, limited-evidence labels (<3 sources), confidence means, representative source-linked evidence, freshness, and coverage from persisted UTC timestamps — never inferred, never from AI/providers. Unassigned evidence appears in an `Unmapped` bucket. No active OpenSpec change remains.

## Completed change

`competitor-gap-analysis` → archived as `openspec/changes/archive/2026-09-30-competitor-gap-analysis/`, with the generated capability spec at `openspec/specs/competitor-gaps/spec.md` (TBD purpose filled in; `source-adapters` TBD purpose also filled in). All requirements and scenarios are satisfied: explicit evidence matrix with unmapped bucket (compare/unmapped), deterministic windows with insufficient/limited handling (equal/sparse windows), read-only project scope with foreign-filter rejection (foreign competitor → 400), plus the matrix view with window/competitor filters, evidence links, and uncertainty/coverage labels. Additive `CompetitorGaps` migration (`competitors` + `source_item_competitors` tables, unique project-scoped indexes). Stale docs refreshed (README, ROADMAP, product-brief, architecture, capture-plan marked STALE pending re-capture); `.gitignore` extended (TestResults, .vs, binlog, logs, e2e reports, temp/sonar) with build outputs confirmed ignored and untracked.

## Verification evidence

- `dotnet format Kairion.slnx --verify-no-changes` — exit 0.
- `dotnet build Kairion.slnx` — 0 errors.
- `dotnet test Kairion.slnx` — 63/63 unit tests and 44/44 integration tests pass (6 unit + 6 integration new for this change).
- `npm run typecheck`, `npm run build`, `npm test` (in `web/`) — typecheck clean, production bundle builds, 1/1 smoke test passes.
- `openspec validate --all --strict --no-interactive` — competitor-gaps, demand-research, and source-adapters specs pass (3 passed, 0 failed).
- `python3 scripts/verify_bootstrap.py` — repository metadata and OpenSpec state consistent.
- V3 note: no live web/provider calls — by design the matrix recomputes from persisted evidence; deterministic fixtures cover 7/30/90-day windows, duplicates, sparse cohorts, unmapped evidence, empty windows, foreign filters, and archived projects.

## Known blockers (outside this repository)

- The portfolio-wide workspace Gate still reports FAIL because of pre-existing issues in sibling projects (for example registered `jenkins-bootstrap` directory missing and unregistered `argoscope`), not because of kairion. The kairion-specific finding count is 0 after registration.

## Uncommitted working-tree state

None. The change follows its exact two-commit lifecycle (implementation/tests/archive commit, then this HANDOFF.md-only pointer/evidence commit).

## Next actions

1. Pick the next OpenSpec change (none is currently packaged) and run its own BFS/DFS/BFS cycle.
2. Re-run the `docs/assets/capture-plan.md` shot-scraper commands to capture the provider table and competitor gap matrix views (artifacts currently STALE at the MVP capture).
3. Report the unrelated portfolio Gate failures to the workspace owner.

Each completed OpenSpec spec/change requires exactly two commits: first the implementation/tests/archive commit, then a HANDOFF.md-only pointer/evidence commit.

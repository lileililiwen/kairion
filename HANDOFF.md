current_spec: additional-source-adapters

# Handoff

## State

The `additional-source-adapters` OpenSpec change (R1–R4) is implemented, verified end-to-end against PostgreSQL 16, archived, and shipped on `main`. Per-project provider configuration with validated bounds, the Hacker News public-search adapter and the configured web-search adapter, normalized provenance-preserving ingestion with canonical-URL idempotency, and per-provider run evidence with failure isolation all run against real code. No active OpenSpec change is committed; the `competitor-gap-analysis` planning package remains an uncommitted working-tree directory (see below).

## Completed change

`additional-source-adapters` → archived as `openspec/changes/archive/2026-09-30-additional-source-adapters/`, with the generated capability spec at `openspec/specs/source-adapters/spec.md`. All R1–R4 requirements and their scenarios are satisfied: providers independently configured/bounded (configure/reject), retrieval with normalized provenance (retrieve valid), idempotent canonical-URL upsert (duplicate URL), and isolated inspectable failures (rate limit/malformed). The `ISourceProvider` contract now returns `SourceBatch` (provider key, candidates, Complete/Partial/Failed/Disabled status, safe diagnostic code, retry-after, retrieved-at); secrets stay in deployment configuration and are never persisted or returned.

## Verification evidence

- `dotnet format Kairion.slnx --verify-no-changes` — exit 0.
- `dotnet build Kairion.slnx` — 0 errors.
- `dotnet test Kairion.slnx` — 57/57 unit tests and 38/38 integration tests pass (16 unit + 12 integration new for this change).
- `npm run typecheck`, `npm run build`, `npm test` (in `web/`) — typecheck clean, production bundle builds, 1/1 smoke test passes.
- `openspec validate --all --strict --no-interactive` — demand-research spec, source-adapters spec, and the working-tree competitor-gap-analysis change pass (3 passed, 0 failed).
- `python3 scripts/verify_bootstrap.py` — repository metadata and OpenSpec state consistent.
- Postgres 16 end-to-end (kairion db): applied `SourceAdapters` migration (`source_ingestion_runs` table + `research_projects.provider_settings_json` default `[]`); created a project with demo-search config; search returned 5 candidates; duplicate re-run kept 5 rows (idempotent); disabled web-search returned `Disabled` run with 0 candidates; non-HTTPS endpoint rejected (400, prior config preserved); unknown provider rejected (422); enabled HN adapter returned 5 live candidates (`Complete`, news.ycombinator.com URLs). E2E rows cleaned up afterward (2 pre-existing projects remain, 0 runs).
- V3 note: no live web-search check — by design the adapter is Disabled until an owner configures an HTTPS endpoint plus deployment credential; no secret was required for any test. HN live check succeeded (see above).

## Known blockers (outside this repository)

- The portfolio-wide workspace Gate still reports FAIL because of pre-existing issues in sibling projects (for example registered `jenkins-bootstrap` directory missing and unregistered `argoscope`), not because of kairion. The kairion-specific finding count is 0 after registration.

## Uncommitted working-tree state

`openspec/changes/competitor-gap-analysis/` is a complete follow-up planning package and is intentionally left uncommitted so this change keeps its exact two-commit lifecycle. It is eligible to become the next active change.

## Next actions

1. Select `competitor-gap-analysis` as the next active change and commit its package.
2. Report the unrelated portfolio Gate failures to the workspace owner.
3. Implement the next package only after its own BFS/DFS/BFS cycle.

Each completed OpenSpec spec/change requires exactly two commits: first the implementation/tests/archive commit, then a HANDOFF.md-only pointer/evidence commit.

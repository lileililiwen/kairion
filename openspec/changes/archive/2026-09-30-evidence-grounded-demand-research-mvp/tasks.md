# Tasks: evidence-grounded demand research MVP

## 1. BFS — Baseline and impact coverage

- [x] **B1** Map proposal requirements R1–R6 to Domain/Application/Infrastructure/API/web, schema/migrations, provider boundaries, failure cases and exact tests; record no affected sibling files.
- [x] **B2** Create test fixtures/contracts for permitted source data, typed AI JSON schema, UTC windows, provider status errors and fake providers before implementation.
- [x] **B3** Confirm .NET 10/React boundaries and inspect published `dotnet-platform-libs` package versions; record adopt/local adapter decision without editing the sibling.
- [x] **B4** Review handoff gate: implementation requires no unresolved language, owner, API, persistence, privacy, failure, or verification decision.

## 2. DFS — Requirement-by-requirement implementation

- [x] **D1 (R1)** Implement research project model, validation, PostgreSQL migration, CRUD API and React create/edit/archive flow.
- [x] **D2 (R2)** Implement `ISourceProvider`, manual URL intake, one configured provider seam, normalization/provenance, uniqueness, and empty/duplicate/policy-denied behaviors.
- [x] **D3 (R3)** Implement BYOK secret configuration and typed screening DTO/schema validation; persist screening/provider versions and bounded idempotent job retries.
- [x] **D4 (R3)** Implement deep-analysis DTO/schema, confidence/range validation, redacted provider failures, and retry without deleting source evidence.
- [x] **D5 (R4)** Implement deduplication, pain clusters, assignment revisions, human review/merge/split, and evidence-linked cluster API/UI.
- [x] **D6 (R5)** Implement deterministic UTC 7/30/90-day counts, zero-baseline/new-signal behavior, versioned trend labels, freshness and as-of fields.
- [x] **D7 (R6)** Implement Opportunity Signal read model/UI from stored cluster evidence; show evidence volume/growth/confidence and “signal is not business validation.”

## 3. BFS — Cross-surface regression and completeness

- [x] **C1** Recheck project lifecycle, duplicate intake, partial provider failure, retries, human overrides, source deletion/update and API/UI consistency.
- [x] **C2** Verify no secrets, unpermitted source payloads, or aggregate values originating from AI are persisted/logged/displayed.
- [x] **C3** Reconcile every R1–R6 scenario against code, migrations, API, UI and tests; remove stubs and update docs/roadmap only with evidenced behavior.

## 4. Verification

- [x] **V1** Run `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`, `npm ci`, `npm run build`, and `npm test`; capture exact results.
- [x] **V2** Run `openspec validate --all --strict --no-interactive` and `python3 scripts/workspace_check.py --root .. --project kairion`; resolve blockers.
- [x] **V3** Run the self-hosted app with synthetic data, verify evidence-board routes and capture a privacy-reviewed screenshot; record revision/time in capture plan.
- [x] **V4** Archive the completed change and commit implementation/tests/archive/generated specs, then commit only `HANDOFF.md` pointer/evidence; stop after exactly two commits.

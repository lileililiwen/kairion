# Tasks: Additional source adapters

## 1. BFS — Baseline and impact coverage
- [x] B1 Map R1–R4 through Core/Application/Infrastructure/API/Web, migration and job callers; verify no source-specific types cross the boundary.
- [x] B2 Add synthetic HN/search fixtures for empty, duplicate, malformed, oversized, timeout, 429 and partial-page cases.
- [x] B3 Confirm provider documentation/policy and deployment secret mechanism; record compatibility without editing shared repos.
- [x] B4 Confirm implementation handoff: .NET 10, exact adapter ownership, canonical URL key, limits and failure transitions.

## 2. DFS — Requirement-by-requirement implementation
- [x] D1 (R1) Add per-project provider configuration DTOs, validation and disabled-by-default persistence/API behavior.
- [x] D2 (R2) Implement HN and configured search adapters with bounded queries, pagination, timeout, retry-after and safe diagnostics.
- [x] D3 (R3) Normalize candidate fields and enforce HTTPS/size/payload limits without persisting raw responses or credentials.
- [x] D4 (R4) Upsert candidates idempotently by project/canonical URL and persist complete/partial/failure run evidence.
- [x] D5 (R1–R4) Add source configuration and provider health controls to the research project UI.

## 3. BFS — Cross-surface regression and completeness
- [x] C1 Verify concurrent duplicate runs, per-provider isolation, restart/retry behavior and last-good evidence retention.
- [x] C2 Verify API, job, persistence and UI agree on provider state, as-of time, partial results and safe errors.
- [x] C3 Reconcile R1–R4, scenarios, migration, tests and docs; remove placeholders and confirm no scraping or lead workflows.

## 4. Verification
- [x] V1 Run format, .NET build/test, frontend install/build/test and strict OpenSpec validation; preserve command output.
- [x] V2 Run synthetic provider integration proving candidates link to original source and counts remain downstream deterministic.
- [x] V3 Record unavailable external provider checks as blocked; no live data or secret is required for tests.

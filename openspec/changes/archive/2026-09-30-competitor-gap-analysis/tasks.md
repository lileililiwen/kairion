# Tasks: Competitor gap analysis

## 1. BFS — Baseline and impact coverage
- [x] B1 Trace MVP competitor identity, cluster membership and evidence query through persistence/API/UI; map R1–R4.
- [x] B2 Create fixed UTC matrix fixtures for duplicates, unmapped items, sparse cohorts, stale evidence and empty windows.
- [x] B3 Confirm comparison uses only project-scoped persisted fields and has no dependency on optional provider adapters.
- [x] B4 Record .NET 10/API/UI ownership, deterministic window boundaries and limited-evidence rule for implementer handoff.

## 2. DFS — Requirement-by-requirement implementation
- [x] D1 (R1) Implement project-scoped competitor-gap query DTO/service and exact UTC window bounds.
- [x] D2 (R2) Implement distinct evidence/source counts, previous-window deltas, freshness and insufficient-data classification.
- [x] D3 (R3) Add read-only API validation, project isolation and representative evidence references.
- [x] D4 (R4) Add matrix view, filters, evidence links and uncertainty/coverage labels.

## 3. BFS — Cross-surface regression and completeness
- [x] C1 Verify reassignment, duplicate observations, archived/missing projects and concurrent reads.
- [x] C2 Verify all UI counts and trend labels match API fixture; no inferred totals or unlinked claims.
- [x] C3 Reconcile R1–R4 across contract, query, API, tests and documentation; remove placeholders.

## 4. Verification
- [x] V1 Run .NET formatting/build/tests, frontend install/build/tests and strict OpenSpec validation.
- [x] V2 Capture deterministic matrix assertions for 7/30/90-day windows, low sample and unmapped evidence.
- [x] V3 Record revision and test output; no live web/provider calls are needed.

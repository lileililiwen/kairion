# competitor-gaps Specification

## Purpose
Provide a deterministic, source-linked comparison of demand evidence across owner-defined competitors.

## ADDED Requirements

### Requirement: Competitor matrix is derived from explicit evidence
The system MUST show project-scoped competitor-by-cluster evidence counts and source-linked representative items using explicit competitor assignment.

#### Scenario: Compare competitors with evidence
- **WHEN** a project contains assigned items for multiple competitors and clusters
- **THEN** the matrix returns distinct item/source counts, timestamps, coverage and links to the underlying evidence

#### Scenario: Evidence is not assigned to a competitor
- **WHEN** an item has no explicit competitor assignment
- **THEN** it appears as unmapped evidence and is not attributed by this feature

### Requirement: Trends disclose their deterministic window and limits
The system MUST calculate changes from stored UTC timestamps and show unavailable or low-sample results without manufacturing a trend.

#### Scenario: Compare equal time windows
- **WHEN** current and preceding windows both contain valid observations
- **THEN** the response reports reproducible counts and their deterministic delta for the selected window

#### Scenario: Window lacks sufficient evidence
- **WHEN** either comparison window is absent or the current cohort has fewer than three distinct items
- **THEN** the result marks insufficient or limited evidence and does not claim an emerging or declining trend

### Requirement: Comparison remains read-only and project-scoped
The API MUST reject invalid or foreign filters and MUST NOT mutate research items or cluster assignments.

#### Scenario: Request a foreign competitor
- **WHEN** a competitor ID does not belong to the selected project
- **THEN** the API returns a validation error and exposes no foreign project data

## Traceability

| Requirement | Proposal | Design | Boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|---|
| R1 Explicit evidence matrix | comparison read model | deterministic matrix DTO | Application/Query; D1,D3 | compare/unmapped | B1,B2,D1,D3,C2,V2 | fixed fixture asserts counts and refs |
| R2 Deterministic trend/limits | evidence comparisons | UTC windows/threshold | Query; D2,D4 | equal/sparse windows | B2,B4,D2,D4,C1,C2,V2 | unit assertions for bounds/deltas |
| R3 Read-only project scope | project-level query | validation and no writes | API; D3 | foreign competitor | B1,D3,C1,V1 | API scope/validation tests |

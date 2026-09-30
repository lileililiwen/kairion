# competitor-gaps Specification

## Purpose
Provide a deterministic, source-linked comparison of demand evidence across owner-defined competitors.
## Requirements
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


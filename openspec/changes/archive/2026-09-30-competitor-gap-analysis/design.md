# Design: Competitor gap analysis

## Implementation boundary
Repository `kairion`, .NET 10 `Kairion.Application`, `Kairion.Infrastructure`, `Kairion.Api`, and React/TypeScript `Kairion.Web`. Reuse MVP research-project competitor metadata, source items, pain-cluster assignments and evidence query contracts. Add a read-only comparison query service/DTO, API endpoint and matrix view. Do not add a second ingestion/cluster pipeline or change source adapters.

## Language and runtime
C# / ASP.NET Core 10, EF Core/PostgreSQL; React + TypeScript. Build/test commands: `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`, `npm ci`, `npm run build`, `npm test`; strict OpenSpec validation at repository root.

## Ownership and shared code
All comparison semantics remain Kairion-owned. The comparable Argoscope feature ranks GitHub repository metrics and is not a compatible reusable dependency. No shared-library modification or cross-project reference.

## Behavioral model
Input is a research project ID, UTC window (7/30/90 days), and optional competitor IDs. Return one row per competitor × cluster, with distinct evidence count, distinct source count, first/last observed UTC, deterministic change versus preceding equal-duration window, cluster confidence summary, representative evidence IDs/URLs, freshness, and coverage. Only evidence with explicit competitor assignment is attributed; unassigned items appear in a separate `Unmapped` bucket and never get inferred from text by this feature. A source item counts once per competitor/cluster regardless of duplicate provider observations. A cluster spanning competitors appears in each assigned cohort. The query is read-only and recomputed from persisted evidence timestamps and assignments.

Low sample rule: fewer than 3 distinct source items is displayed as “Limited evidence”; numerical change is null when either current or prior-window denominator is absent/zero. Confidence is the deterministic mean of validated item pain confidence, accompanied by sample count; it is not a probability of market demand. Archived projects return 404; an empty project returns a valid empty matrix.

## Contract and compatibility
`GET /api/research-projects/{projectId}/competitor-gaps?window=7d|30d|90d&competitorId=...` returns `asOfUtc`, `windowStartUtc`, `previousWindowStartUtc`, `coverage`, competitor rows and cluster rows. Each cell includes counts, nullable delta/percentage, classification `Emerging|Stable|Declining|InsufficientData`, `limitedEvidence`, confidence mean, representative evidence refs and freshness. Invalid windows/foreign competitor IDs return 400; missing project 404. Additive endpoint only; no schema migration unless the current competitor identity lacks stable ID, in which case an additive unique project-scoped ID migration is required. UI provides filter and link-through to evidence; labels say “signals” and “limited evidence”.

## Failure and boundary policy
| Condition | Behavior |
|---|---|
| No results | 200 with empty rows and zero coverage |
| Missing prior window | Null delta; `InsufficientData` |
| Fewer than 3 items | Show counts/evidence and limited-evidence label; no strong trend label |
| Missing competitor assignment | Aggregate under `Unmapped`; never guess |
| Stale source evidence | Keep counts but mark stale using MVP freshness policy |
| Concurrent reassignment | Query observes a consistent database snapshot; next request reflects new assignment |
| Invalid/foreign filter | 400 with field-level validation error |

## Verification oracle
Pure query tests use fixed UTC timestamps and known competitor/cluster assignments to assert exact distinct counts, windows, deltas, low-sample state and unmapped bucket. PostgreSQL tests assert distinctness and consistent project scoping. API tests cover invalid/foreign IDs, absent project and empty data. UI tests verify evidence links, coverage and uncertainty labels. Build/test and strict validation commands above are the completion oracle.

## Decision ledger
- Data counts and trend deltas are computed in SQL/application code, never LLM-generated.
- Competitor attribution is explicitly assigned by owner or upstream MVP analysis; this capability does not infer it.
- Comparison requires only MVP data and is independent from adapter expansion.
- Deferred: feature-level scraping, automated discovery, cross-project benchmarks, opportunity recommendations.

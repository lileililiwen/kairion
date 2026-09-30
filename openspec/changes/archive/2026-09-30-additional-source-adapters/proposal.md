# Proposal: Additional source adapters

## Why
Research coverage is limited when a project can ingest only its initial source set. Owners need independently configurable adapters while retaining source provenance and the existing evidence lifecycle.

## What Changes
- Add provider adapters behind the MVP `ISourceProvider` contract, initially for Hacker News public search and one explicitly configured web-search provider.
- Add per-project adapter configuration, bounded query execution, and normalized candidate ingestion.
- Preserve original URL, provider identity, observed/published timestamps, and retrieval status.

## Package Boundary and Split Assessment
This package owns source retrieval and normalization only. Competitor gap comparison has a separate aggregation/read model and independent oracle, so it is covered by `competitor-gap-analysis`. Dependency order: `evidence-grounded-demand-research-mvp` → this package; the gap analysis consumes normalized evidence and can proceed independently. Hosted SaaS, Reddit scraping, messaging, and source-specific product semantics are excluded.

| Package | Single outcome | Owner/project and language | Boundary/contract | Depends on | Independent oracle |
|---|---|---|---|---|---|
| evidence-grounded-demand-research-mvp | Evidence-linked demand research | Kairion; C#/.NET 10, React/TS | Existing research/evidence contract | — | MVP end-to-end fixture |
| additional-source-adapters | Additional policy-compliant sources | Kairion; C#/.NET 10 | `ISourceProvider` normalized candidate contract | MVP | Provider contract fixtures and idempotent ingestion |
| competitor-gap-analysis | Evidence-backed competitor gap matrix | Kairion; C#/.NET 10, React/TS | Research competitor identity and cluster read model | MVP | Fixed evidence fixture yields reproducible matrix |

## Sibling and Shared Architecture Reconnaissance

| Candidate | Evidence path/symbol | Reusable code/config/architecture | Compatibility gap | Owner and release boundary | Decision |
|---|---|---|---|---|---|
| dotnet-platform-libs | `../dotnet-platform-libs`; MVP architecture checkpoint | .NET 10 AI/job contracts only | No source retrieval semantics or source policy | Shared library owns provider plumbing; Kairion owns source adapters | adapt through a generic adapter |
| Forge templates | `../forge`; prior bootstrap reconnaissance | Generic project scaffolding | No demand-research source model | Forge release independent | keep local |

## BFS Impact Map
- Affected: Kairion source-provider implementations, project source configuration, candidate-ingestion application service, persistence and research UI source controls.
- Contracts: MVP `ISourceProvider`, normalized candidate, provider result/error and provenance records remain authoritative.
- Integrations: Hacker News public search API and a configured web-search provider. Provider terms/quotas are configuration/deployment constraints; no HTML scraping or access-control bypass.
- Failure boundaries: timeout, rate limit, malformed response, duplicate URL, provider disabled, and partial page results are visible and retryable without losing accepted candidates.
- Unchanged: research analysis, clustering/trend arithmetic, auth model, AI provider, sibling repos and GitHub behavior.
- Shared assessment: provider transport may use existing .NET packages after implementation compatibility review; no shared-library change is in scope.

## Capabilities
- `source-adapters`: retrieve and normalize candidates through enabled, bounded, policy-compliant providers.

## Non-goals
Reddit scraping, browser automation, unrestricted crawling, lead generation, source-specific demand scoring, competitor comparison, managed credentials, and hosted operation.

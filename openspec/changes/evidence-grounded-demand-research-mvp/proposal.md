# Proposal: evidence-grounded demand research MVP

## Why

Founders need a way to distinguish recurring, growing product problems from a pile of discussion links. Kairion must show evidence behind each cluster and opportunity signal so AI summaries remain inspectable.

## What Changes

- Add research projects, candidate intake through a provider boundary, staged screening/deep analysis, deduplication, pain clusters, evidence-linked summaries, deterministic trends, and Opportunity Signal cards.
- Add BYOK AI configuration with typed, JSON-schema-validated outputs and visible provider failures.
- Deliver a self-hosted ASP.NET Core 10 + React/TypeScript app using PostgreSQL, Hangfire, and Docker.

## Package Boundary and Split Assessment

| Package | Single outcome | Owner/project and language | Boundary/contract | Depends on | Independent oracle |
|---|---|---|---|---|---|
| `evidence-grounded-demand-research-mvp` | Evidence-linked demand signals for one self-hosted owner | Kairion; C#/.NET 10 and TypeScript/React | Research project → source item → analysis → cluster → evidence/trend/opportunity read API | Product foundation | End-to-end fixture shows each signal with its original source; counts match stored UTC observations |
| `additional-source-adapters-and-competitor-gaps` | Expand source coverage and compare competitor problem gaps | Kairion; C#/.NET 10 and TypeScript | Versioned `ISourceProvider` plus competitor grouping | MVP | Adapter contract suite and cross-competitor evidence matrix |
| `hosted-collaboration-and-managed-ai` | Multi-user hosted operation and managed provider billing | Kairion; separate service lifecycle | Account/tenant/billing and secret custody boundary | MVP and security review | Tenant isolation, billing and deployment evidence |

The MVP is a vertical slice because its entities share one evidence lifecycle and must become useful together. Additional provider/catalog growth and hosted SaaS have separate owners, security boundaries, and acceptance oracles and are excluded.

## Sibling and Shared Architecture Reconnaissance

| Candidate | Evidence path/symbol | Reusable code/config/architecture | Compatibility gap | Owner and release boundary | Decision |
|---|---|---|---|---|---|
| dotnet-platform-libs | `src/Platform.Ai.Contracts`, `src/Platform.Ai.OpenAiCompatible`, `src/Platform.Ai.Anthropic`, `src/Platform.Ai.Ollama` | Provider-neutral AI contracts and provider adapters target .NET 10. | Must confirm published package versions, schema guarantees, and redaction behavior before consumption. | Shared library owns cross-product provider contracts; Kairion owns demand/evidence semantics. | **deferred investigation** |
| dotnet-platform-libs | `src/Platform.Jobs.Hangfire` | Hangfire dispatcher/recurring job contracts and PostgreSQL storage option. | Need to verify package version, serialization and retry behavior against Kairion's idempotency model. | Shared job adapter released independently; Kairion owns processing jobs and idempotency. | **deferred investigation** |
| Forge | `profiles/aspnet-web` | Deterministic ASP.NET scaffold and profile workflow. | Current pinned profile targets .NET 8, while Kairion requires .NET 10. | Forge owns versioned scaffolding; not a Kairion dependency. | **keep local** |

No sibling will be edited. Before implementation, verify package manifests/release tags and either adopt compatible published packages or keep a thin Kairion adapter; do not copy shared implementation source.

## BFS Impact Map

| Surface | Impact and invariant |
|---|---|
| Actors/flow | Single self-hosted owner creates a research brief, reviews evidence and corrects clustering. |
| Contracts/data | Research project, source item/provenance, screening, analysis, cluster/assignment, observations, trend and opportunity DTOs. Every derived item links to evidence. |
| Integrations/config | One configured source/search adapter and BYOK AI provider; keys are external secrets. |
| Failures | Provider timeout/quota/denial/invalid schema remains observable; retry is idempotent and cannot delete evidence. |
| Privacy/security | No unauthorized scraping or access-control bypass; minimize personal data; redact secrets and raw provider errors. |
| Verification | Unit tests for validation/statistics, adapter contract tests, PostgreSQL integration/migrations, API/UI end-to-end fixture and strict OpenSpec validation. |
| Unchanged | No CRM, outreach, billing, managed AI, broad source catalog, multi-tenant SaaS or business-validation decision. |

## Capabilities

- Research projects and normalized source evidence.
- Cost-aware staged screening and typed deep analysis.
- Deduplicated pain clusters, evidence board and human corrections.
- Deterministic 7/30/90-day trends and qualified opportunity signals.

## Non-goals

Lead generation, direct messages, CRM, sales automation, scraping behind authentication, bypassing provider limits, AI-generated aggregate counts, automatic business decisions, billing, managed AI, multi-user tenancy, and dozens of integrations.

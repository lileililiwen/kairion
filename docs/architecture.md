# Kairion architecture decisions

Status: implemented baseline — the demand-research MVP, source adapters, and competitor gap matrix run on this stack and are verified by .NET + web tests and strict OpenSpec validation.

## ADR 0001 — Self-hosted .NET and React application

- **Decision:** ASP.NET Core 10 API, React + TypeScript web client, PostgreSQL, Hangfire, Docker. Keep `Kairion.Domain`, `Kairion.Application`, `Kairion.Infrastructure`, `Kairion.Api`, and `web/` boundaries in one repository/solution.
- **Reason:** the product is a long-running web/data pipeline with provider APIs and scheduled work; align with the user's selected stack and keep AI adapters behind typed interfaces.
- **Rejected:** Python-only runtime (not required for the described workflows); adopting Forge's `aspnet-web@0.1.0` generator (it targets .NET 8, not the requested .NET 10).
- **Consequence:** create the .NET 10 solution and React app explicitly during implementation; don't claim Forge scaffold support for this target.

## ADR 0002 — Provider-neutral source and AI boundaries

- **Source contract:** `ISourceProvider.SearchAsync(SourceQuery, CancellationToken)` and `GetAsync(SourceItemId, CancellationToken)` return normalized candidate records plus provenance and provider status. First release accepts manual URLs plus one configured search/source adapter. Provider-specific raw payloads stay in the adapter; only permitted fields enter product storage.
- **AI contract:** `IAiProvider.AnalyzeAsync<TRequest,TResponse>(request, JsonSchema, CancellationToken)` returns typed parsed output, provider/model/version metadata, token/cost metadata when available, and a classified error. JSON schema validation occurs before persistence. `IEmbeddingProvider` is optional and is not required for the MVP's deterministic/human-reviewable clustering.
- **Failure:** timeout, quota, network, refusal, and invalid schema are distinct retryable/non-retryable outcomes; no failure deletes source evidence. Bounded retries are idempotent by candidate + analysis-version key.

## ADR 0003 — Persist evidence; compute statistics in code

Store research project, source item, screening result, deep analysis, cluster, cluster assignment, observation, project-scoped competitor, and explicit source-item competitor-assignment timestamps in PostgreSQL. Unique source identity is `(provider, external_id)` with canonical URL as a deduplication hint, not the sole identity. Competitor identity is `(project_id, normalized_name)` with a stable surrogate id; gap counts query source items by effective UTC date within the selected window and never infer attribution. Every AI result is versioned and points to its source item. Cluster edits preserve audit history. Trend counts query observations by UTC time window; AI cannot write count or trend fields.

## Shared-library decision

Inspect `dotnet-platform-libs` when implementation begins. It owns .NET 10 `Platform.Ai.Contracts`, `Platform.Ai.OpenAiCompatible`, `Platform.Ai.Anthropic`, `Platform.Ai.Ollama`, and `Platform.Jobs.Hangfire` packages. Decision: **evaluated per change, keep local** — no compatible demand-cluster or competitor-semantics capability exists there, so no project reference is added and that sibling is never modified. Kairion retains source/evidence/domain contracts because those are product-specific. Dependency direction stays from Kairion application/infrastructure to optional versioned platform packages, never from the platform into Kairion.

## Security and privacy

Single-owner self-hosted MVP. Keep AI/search secrets in environment or deployment secret storage; redact configuration and provider exceptions. Minimize stored personal information and excerpts; retain canonical source links, timestamps, provider provenance, and only fields permitted by source policy. Auth, multi-tenant hosting, billing, and managed secrets are deferred and must not be implied by the MVP.

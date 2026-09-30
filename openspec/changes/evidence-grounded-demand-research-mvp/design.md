# Design: evidence-grounded demand research MVP

## Implementation boundary

Repository `kairion`. Create a .NET solution with `src/Kairion.Domain`, `src/Kairion.Application`, `src/Kairion.Infrastructure`, `src/Kairion.Api`, `tests/Kairion.UnitTests`, `tests/Kairion.IntegrationTests`, plus `web/` React application. Domain owns research/evidence/cluster/value objects; Application owns use cases and provider-neutral interfaces; Infrastructure owns EF Core/PostgreSQL, Hangfire, and source/AI adapters; API owns HTTP/auth/config composition; web owns research setup, evidence board, cluster review and opportunity views. Do not edit Forge or `dotnet-platform-libs`.

## Language and runtime

C# on ASP.NET Core 10 / .NET 10; React + TypeScript on Node 20+; PostgreSQL; Hangfire; Docker Compose for local self-hosting. Commands: `dotnet build`, `dotnet test`, `npm ci`, `npm run build`, `npm test`, `openspec validate --all --strict --no-interactive`, and Workspace Governance checker. Use UTC timestamps, async I/O, cancellation tokens, EF Core migrations, explicit DTOs, and no credentials in checked-in config. Current bootstrap has SDK 10.0.400 available but no solution or web package.

## Ownership and shared code

Kairion owns domain models, source policy, `ISourceProvider`, `IAiProvider` application facade, typed screening/analysis DTOs, trend computation, and opportunity rules. Platform package evaluation is a pre-implementation task: `Platform.Ai.Contracts`/provider packages and `Platform.Jobs.Hangfire` may be adopted only through compatible published versions. Keep local adapters behind the same interfaces if compatibility is absent. Never add a direct application dependency on vendor SDKs or use shared packages as the domain model.

## Behavioral and persistence model

| Entity | Required fields/invariants |
|---|---|
| ResearchProject | Id, title, brief type/value, topics, competitors, source configuration, created/updated UTC, state. Archive is reversible; hard deletion is an explicit owner action. |
| SourceItem | ProjectId, provider id, external id, canonical URL, title/excerpt permitted by source policy, published/observed UTC, provenance, source status. Unique `(provider, external_id)` per project. |
| ScreeningResult | SourceItemId, analysis version, relevance/pain/commercial hint in [0,1], spam flag, decision, provider/model, created UTC, validated schema. |
| DeepAnalysis | SourceItemId, schema version, problem/context/current solution/dissatisfaction/workaround/desired outcome/category, optional price sensitivity, pain strength/confidence in [0,1], provider/model, status. |
| PainCluster/Assignment | ProjectId, label/category/summary, version and review state; assignment references source and analysis. Human correction creates an auditable revision and wins over later AI suggestions. |
| OpportunitySignal | ClusterId, deterministic evidence volume/growth/workaround summary, AI-assisted explanation/confidence, generated UTC and source evidence links. Explicitly informational. |

Intake is idempotent by project/provider/external id; canonical URL is a secondary duplicate hint. Jobs are idempotent by source-item id + prompt/schema version. `Queued → Screened → Analyzed → Clustered` is not a destructive linear status: source evidence is immutable except for permitted metadata refresh, and a failed stage can retry without resetting completed valid stages. Human review can mark false positive, edit labels, or merge/split clusters with audit records.

## Contracts and API

- `ISourceProvider.SearchAsync(SourceQuery, CancellationToken)` and `FetchAsync(SourceReference, CancellationToken)` return `SourceFetchResult` with provider id, canonical URL, permitted content fields, timestamps, and `ProviderObservation`.
- `IAiProvider.AnalyzeAsync<TInput,TOutput>(TInput input, JsonSchema schema, CancellationToken)` returns typed output plus provider/model/schema/version and usage metadata; application validates schema and numeric ranges before persistence.
- API v1 routes: `/api/v1/research-projects`, `/api/v1/research-projects/{id}/candidates`, `/api/v1/research-projects/{id}/clusters`, `/api/v1/clusters/{id}/evidence`, `/api/v1/research-projects/{id}/trends?window=7d|30d|90d`, `/api/v1/opportunity-signals/{id}`. Mutations use validation problem details; absent/foreign ids return 404.
- Provider statuses: `available`, `rate_limited`, `unauthorized`, `unavailable`, `timed_out`, `invalid_response`, `policy_denied`. Do not expose secret-bearing provider messages.
- Config names: `ConnectionStrings__Kairion`, `Providers__Source__*`, and `Providers__Ai__*`; secret values are deployment environment secrets, not project entity fields.

## Trend and evidence rules

Counts group distinct source items by first-seen published/observed UTC date using documented precedence: valid source published timestamp, else first observed timestamp. Compute window counts and prior-window delta in SQL/application code. If the prior count is zero, percent growth is `null` with `new_signal=true`; never divide by zero. Emerging/Stable/Declining thresholds are versioned deterministic configuration and covered by boundary tests. All aggregate responses include window, as-of UTC, included item count and data freshness. AI cannot supply numeric aggregates.

## Failure, privacy and retry behavior

Malformed query: 400 validation response, no job. Duplicate candidate: return existing item, no duplicate row. Unauthorized source/provider: mark the attempt and provider status; preserve existing data; do not retry until credentials/config change. Rate limit/timeout/transient network: bounded exponential retry with jitter and provider `Retry-After` respect; exhaustion produces visible failed stage and manual retry. Invalid AI JSON/schema: reject result, store redacted failure metadata, retain candidate for retry. Empty search: successful zero-result state. PostgreSQL transaction failure: roll back item/analysis write; Hangfire retry uses idempotency key. Cancellation is propagated. Secrets and unpermitted raw payloads never enter logs or evidence API.

## Verification oracle

Unit tests assert range/schema validation, duplicate identity, deterministic windows, zero baselines, threshold boundaries, confidence ranges, and no-AI-count invariant. Adapter contract fixtures cover success, empty, rate-limited, denied, timeout, malformed response, and cancellation. PostgreSQL integration tests apply migrations, enforce unique constraints, and prove retries do not duplicate rows. End-to-end test seeds synthetic source items, screens/analyzes with fake providers, reviews a cluster, and asserts evidence links/counts/trends/opportunity labels. Web tests prove sources and evidence are visible, failures are disclosed, and signals say they are not validation. Run all commands named in the runtime section and strict OpenSpec/Governance checks before acceptance.

## Decision ledger

- Self-hosted single-owner, BYOK, Postgres, Hangfire, Docker and React are chosen from the approved brief.
- First intake is URL import plus one configured provider; Reddit/HN/other integrations require permitted access and are separate adapters.
- No embedding store/vector database is required; first clustering uses typed AI proposals with human review and persisted assignments.
- Platform package consumption is deferred until published compatibility is inspected; it is not a blocker to local provider contracts.
- Authentication/tenancy/billing and managed AI are deferred; do not present these as MVP features.

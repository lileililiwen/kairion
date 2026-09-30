# Design: Additional source adapters

## Implementation boundary
Repository `kairion`; .NET 10 projects `Kairion.Core`, `Kairion.Application`, `Kairion.Infrastructure`, `Kairion.Api`, plus React/TypeScript `Kairion.Web`. Inspect and extend the MVP `ISourceProvider`, candidate-ingestion service, source configuration DTOs, EF Core mappings/migrations, source settings API and project source settings UI. Do not change shared repos, analysis semantics, or MVP contracts incompatibly.

## Language and runtime
C# on ASP.NET Core 10, EF Core/PostgreSQL, Hangfire, React + TypeScript, Docker. Verification commands: `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`, `npm ci`, `npm run build`, `npm test`, and `openspec validate --all --strict --no-interactive` from repository root.

## Ownership and shared code
Kairion owns source-domain DTOs, provider adapters and policy. Keep `ISourceProvider` local in Kairion.Core/Application; do not introduce a shared package for one consumer. Existing dotnet-platform-libs AI/job packages do not own source retrieval. Any transport dependency is an infrastructure implementation detail and cannot leak provider SDK types into API/domain.

## Behavioral model
`SourceConfiguration` contains provider key, enabled flag, non-secret options, query/page limits and credential reference. Secrets remain in deployment secret storage, never PostgreSQL/API responses. Each request has a stable run ID and project ID. Adapter returns zero or more normalized candidates plus a terminal/partial status. Ingestion upserts by `(projectId, canonicalUrl)` while retaining first-seen and most-recent-seen timestamps; duplicate retrieval updates provenance/last-seen without duplicating analysis. Each provider is scheduled/invoked separately; one provider failure does not discard another provider's accepted candidates.

Initial adapters are HN Search API and one configured web-search provider using documented APIs. The web-search adapter is selected through a concrete OpenAI-compatible search endpoint configured by URL and deployment credential. No generic page crawler is implemented. Query and response sizes are bounded (maximum 5 queries/run, 50 results/query, 1 MiB response); provider pagination is capped. A per-provider concurrency limit of 1 and configured minimum interval prevent burst collection.

## Contract and compatibility
`ISourceProvider.SearchAsync(SourceQuery, CancellationToken) -> SourceBatch`; `SourceBatch` includes provider key, candidates, `Complete|Partial|Unavailable`, safe diagnostic code, retry-after UTC, and retrieved-at UTC. Candidate fields: canonical source URL, title, excerpt, source-native ID, published-at nullable, observed-at UTC, author handle nullable, and provider key. Only public metadata/excerpts within provider contract are persisted. API configuration exposes provider key, enabled, limits and health; writes reject unknown keys, invalid HTTPS endpoints, limits outside 1..5 queries and 1..50 results, and secrets. Existing clients remain compatible because source config fields are additive and omitted providers default disabled.

## Failure and boundary policy
| Condition | Behavior |
|---|---|
| Disabled provider | Skip; report `Disabled`, no error |
| Empty successful result | Persist run status `Complete`, zero candidates |
| Duplicate canonical URL | Idempotent update; one candidate row |
| Timeout / 429 / 5xx | Persist safe failure status; honor retry-after; bounded exponential retry, max 3 attempts |
| Invalid payload / non-HTTPS configured endpoint | Reject config or mark provider failed; no raw payload persistence |
| Partial page failure | Keep validated candidates; mark run `Partial` |
| Provider unavailable | Other providers continue; existing evidence remains visible |
| Provider changes/deleted item | Retain original provenance and mark last-seen/status; do not silently erase evidence |

## Verification oracle
Unit tests with recorded, synthetic provider responses assert URL normalization, payload validation, size/page caps, retry policy and secret redaction. Adapter contract tests assert the common result shape. PostgreSQL integration tests assert unique upsert, run state, partial persistence and concurrent duplicate idempotency. API tests assert disabled/default behavior and validation errors. UI tests assert source toggles, limits and health/failure display. Build and test commands above plus strict OpenSpec validation are required before tasks are checked.

## Decision ledger
- Use public documented HN search and one configured API-based search provider; no scraping.
- Provider keys default disabled; owner explicitly configures endpoint and credentials.
- Keep competitor gap analysis separate; this change produces only normalized candidates.
- Deferred: source-specific ranking, Reddit adapter, crawler, OAuth, shared source library.

# Kairion

**Open-source AI-powered demand intelligence for founders.** Kairion is a self-hosted research workspace that turns public discussion evidence into traceable problem clusters, opportunity signals, and competitor gap matrices.

> Status: implemented on `main` — evidence-grounded demand research MVP, additional source adapters (Hacker News + configured web search with bounded provenance-preserving ingestion), and the competitor gap analysis matrix are built, tested (63 unit + 44 integration + web smoke), and strictly OpenSpec-validated.

## Product boundary

Create a research project around a market, product category, competitor, or question. Candidate discussions are collected through explicit source providers, screened, analyzed, deduplicated, clustered, and shown with links to their source evidence. Counts and trends are calculated by application code; AI proposes semantic labels and explanations, never authoritative totals.

The first usable release targets a self-hosted single-owner deployment with PostgreSQL, Hangfire, and user-supplied AI credentials (BYOK). The backend is ASP.NET Core 10 and the primary experience is a React + TypeScript web application. Docker is the deployment format.

## MVP

- Research projects with topic, product/category, competitor, or question inputs.
- A replaceable source-provider contract. Manual URL intake plus independently configured per-project providers (demo search, Hacker News public search, configured HTTPS web search); no unauthorized scraping.
- Cheap relevance/spam/pain screening followed by structured deep analysis for retained candidates.
- Deduplication, pain clusters, representative evidence, source links, confidence, and deterministic 7/30/90-day counts and trends.
- Opportunity Signal cards with evidence volume, growth, alternatives/workarounds, and an explicit “signal is not validation” boundary.
- Competitor gap matrix: deterministic project-scoped competitor-by-cluster signals with stable owner-managed competitor identity, explicit assignment, an `Unmapped` bucket (never inferred), previous-window deltas, limited-evidence labels, and source-linked representative evidence.
- BYOK provider configuration behind typed provider interfaces and schema-validated JSON output.

## Deferred

CRM/lead generation, outreach, automated messaging, billing, managed AI, and claims of market validation are outside the MVP. See [the product brief](docs/product-brief.md) and [roadmap](ROADMAP.md).

## Repository map

- `openspec/specs/` — capability specs (`demand-research`, `source-adapters`, `competitor-gaps`).
- `openspec/changes/archive/` — completed implementation packages with evidence.
- `docs/architecture.md` — stack and ownership decisions.
- `HANDOFF.md` — current change and evidence status.

## Development

Verification is `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test` for the .NET solution and `npm run typecheck`, `npm run build`, `npm test` in `web/`, plus strict OpenSpec validation (`openspec validate --all --strict --no-interactive`) and `python3 scripts/verify_bootstrap.py`. Counts and trends are computed by application code from persisted UTC observations; AI output is typed, schema-validated, and never the source of totals.

## License

MIT. See [LICENSE](LICENSE).

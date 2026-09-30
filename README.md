# Kairion

**Open-source AI-powered demand intelligence for founders.** Kairion is planned as a self-hosted research workspace that turns public discussion evidence into traceable problem clusters and opportunity signals.

> Status: planning and repository bootstrap only. No application runtime, data source integration, or AI analysis is implemented yet.

## Product boundary

Create a research project around a market, product category, competitor, or question. Candidate discussions are collected through explicit source providers, screened, analyzed, deduplicated, clustered, and shown with links to their source evidence. Counts and trends are calculated by application code; AI proposes semantic labels and explanations, never authoritative totals.

The first usable release targets a self-hosted single-owner deployment with PostgreSQL, Hangfire, and user-supplied AI credentials (BYOK). The backend is ASP.NET Core 10 and the primary experience is a React + TypeScript web application. Docker is the deployment format. These are planned decisions, not implemented or runtime-verified facts.

## MVP

- Research projects with topic, product/category, competitor, or question inputs.
- A replaceable source-provider contract. Initial collection is limited to explicit user-supplied candidate URLs and one configured search/source provider; no unauthorized scraping.
- Cheap relevance/spam/pain screening followed by structured deep analysis for retained candidates.
- Deduplication, pain clusters, representative evidence, source links, confidence, and deterministic 7/30/90-day counts and trends.
- Opportunity Signal cards with evidence volume, growth, alternatives/workarounds, and an explicit “signal is not validation” boundary.
- BYOK provider configuration behind typed provider interfaces and schema-validated JSON output.

## Deferred

Broad source coverage, competitor gap matrices, CRM/lead generation, outreach, automated messaging, billing, managed AI, and claims of market validation are outside the MVP. See [the product brief](docs/product-brief.md) and [roadmap](ROADMAP.md).

## Repository map

- `openspec/changes/evidence-grounded-demand-research-mvp/` — first implementation package.
- `docs/architecture.md` — stack and ownership decisions.
- `HANDOFF.md` — current change and evidence status.

## Development

The application has not been scaffolded. Planned verification after implementation is `dotnet build` for the .NET solution and `npm run build` for the React application, plus the project’s tests and strict OpenSpec validation. No build or runtime result is claimed by this bootstrap.

## License

MIT. See [LICENSE](LICENSE).

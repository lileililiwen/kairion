# Kairion roadmap

Status: planning only; no product runtime exists. Release targets below are dependency order, not delivery promises.

| Order | Outcome | Depends on | Acceptance boundary |
|---|---|---|---|
| 1 | Product foundation and safe provider contracts | — | Versioned data/API contracts, configuration, secret handling, migration and local deployment decisions are reviewed. |
| 2 | Research projects and candidate intake | 1 | A user can create/edit/archive a research project and ingest URL candidates through the source-provider boundary with provenance. |
| 3 | Staged AI screening and structured analysis | 2 | Candidate screening precedes expensive analysis; schema-invalid/provider-failed results are visible and retryable. |
| 4 | Deduplication, pain clusters, and Evidence Board | 3 | Cluster claims link to retained original evidence; merge/split and stale evidence are visible. |
| 5 | Deterministic trends and Opportunity Signals | 4 | Counts and 7/30/90-day trend labels are reproducible from stored timestamps; AI cannot supply totals. |
| 6 | Additional source adapters and competitor gap analysis | 5 | Each source is enabled/configured independently and evidence remains source-linked. |

## Deferred scope

CRM, lead export, direct messaging, sales automation, dozens of social sources, hosted SaaS, billing, managed AI, and business-validation claims remain deferred. Provider contracts should permit additions without coupling product semantics to one vendor.

## Shared capability checkpoint

`dotnet-platform-libs` already owns .NET 10 contracts/adapters for AI providers and Hangfire jobs. Kairion should evaluate published package compatibility when implementation begins; this bootstrap does not add a project reference or change the shared repository. Research models, source policy, and analysis semantics remain Kairion-owned.

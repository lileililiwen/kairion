# Kairion roadmap

Status: rows 1–6 are implemented on `main` and verified (63 unit + 44 integration tests, web typecheck/build/smoke, strict OpenSpec validation). Targets below remain dependency order, not delivery promises.

| Order | Outcome | Depends on | Acceptance boundary | Status |
|---|---|---|---|---|
| 1 | Product foundation and safe provider contracts | — | Versioned data/API contracts, configuration, secret handling, migration and local deployment decisions are reviewed. | done |
| 2 | Research projects and candidate intake | 1 | A user can create/edit/archive a research project and ingest URL candidates through the source-provider boundary with provenance. | done |
| 3 | Staged AI screening and structured analysis | 2 | Candidate screening precedes expensive analysis; schema-invalid/provider-failed results are visible and retryable. | done |
| 4 | Deduplication, pain clusters, and Evidence Board | 3 | Cluster claims link to retained original evidence; merge/split and stale evidence are visible. | done |
| 5 | Deterministic trends and Opportunity Signals | 4 | Counts and 7/30/90-day trend labels are reproducible from stored timestamps; AI cannot supply totals. | done |
| 6 | Additional source adapters and competitor gap analysis | 5 | Each source is enabled/configured independently and evidence remains source-linked; the competitor matrix compares explicit evidence across owner-defined competitors with an Unmapped bucket. | done |

## Deferred scope

CRM, lead export, direct messaging, sales automation, dozens of social sources, hosted SaaS, billing, managed AI, and business-validation claims remain deferred. Provider contracts should permit additions without coupling product semantics to one vendor.

## Shared capability checkpoint

`dotnet-platform-libs` owns .NET 10 contracts/adapters for AI providers and Hangfire jobs. Evaluated per change (including competitor-gap-analysis reconnaissance): no compatible demand-cluster or competitor-semantics capability exists, so product semantics stay local in Kairion with no project reference or shared-repo modification. Dependency direction stays from Kairion application/infrastructure to optional versioned platform packages, never from the platform into Kairion.

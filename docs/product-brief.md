# Kairion product brief

## Purpose

Kairion is an open-source AI-powered demand intelligence workspace for founders. It studies public, lawfully accessible discussions to identify recurring unmet needs and emerging problems. It is not a buyer finder, lead-generation product, CRM, or business-validation oracle.

## User inputs and flow

Users create a research project from a market/domain, product category, competitor, or question. A project records topics, included competitors, configured sources, query strategy, date windows, and provider settings. Candidate discovery and processing follow:

```text
research brief → source-provider search/import → normalization and deduplication
→ inexpensive relevance/spam/pain screening → structured deep analysis
→ semantic clustering → evidence board → deterministic trends → opportunity signals
```

Query generation can propose searches for alternatives, pricing complaints, missing features, migration intent, workarounds, performance, and privacy. Query generation is an input helper, not the product's primary value.

## MVP behavior

- Research projects and source-provider abstraction; start with user-submitted candidate URLs plus one configured search/source provider.
- Normalize each candidate to stable source identity, canonical URL, source name, observed/published timestamps, and permitted excerpt/metadata.
- Cheap screening returns typed relevance, pain, commercial-intent hints, and spam classification. Retain decision, model/provider version, and failure evidence.
- Deep analysis for retained candidates returns problem, category, trigger/context, current solution, dissatisfaction, workaround, desired outcome, price sensitivity, pain strength, and confidence.
- Deduplication and clusters link every summary to original evidence. Human review can correct classification and cluster assignment; AI does not silently overwrite human decisions.
- Evidence board shows summary, count, first/last observed, trend, representative evidence links, source, and confidence.
- Trend windows 7/30/90 days and Emerging/Stable/Declining labels are computed by program logic from persisted dated observations. Zero-baseline percentage growth is represented as unavailable/new signal, not infinity.
- Opportunity Signal cards include evidence volume, growth, alternatives/workarounds, and confidence. Every card says a signal is not business validation.
- BYOK model/search credentials are configuration secrets, never persisted with project content or logged. Provider outputs use versioned JSON schemas and typed DTO validation.

## Evidence and trust rules

Every displayed claim resolves to source evidence and records provenance. Do not scrape behind login, bypass access controls, evade rate limits, or collect more personal data than needed. Use provider terms and authorized access; store only permitted text/metadata. Provider errors and stale source data stay visible. Aggregates are calculated from stored observations, not supplied by AI.

## Non-goals and later opportunities

No CRM, lead export, outreach, auto-DM, sales automation, dozens of source integrations, managed AI, billing, or automated business decision. Competitor gap matrices, richer source catalogs, hosted SaaS, and collaboration can be proposed after the evidence-centered MVP is usable.

## Release boundary

Self-hosted, single-owner community edition first; PostgreSQL and Hangfire; Docker packaging; ASP.NET Core 10 API and React + TypeScript web application. Users pay their own selected LLM/search provider. Runtime, API, source access, and deployment are not implemented by repository bootstrap.

# Demand research

## ADDED Requirements

### Requirement: Research projects preserve an explicit research brief

The system MUST let the owner create, update, list, and archive a research project with a brief kind (market, category, competitor, or question), brief text, topics, configured sources, and UTC timestamps. Invalid or empty brief input MUST be rejected without creating a project.

#### Scenario: Create a project
- **WHEN** the owner submits a supported brief kind and non-empty brief text
- **THEN** the system creates one active project and returns its stable id and stored source configuration

#### Scenario: Reject an empty brief
- **WHEN** the owner submits whitespace-only brief text
- **THEN** the system returns a field validation error and persists no project

### Requirement: Candidate intake records provenance and is idempotent

The system MUST normalize candidates from manual URL submission or an enabled `ISourceProvider`, retain canonical URL/provider identity and observed/published timestamps, and link all later analysis to the source item. Repeated `(project, provider, external_id)` input MUST return the existing source item without duplication. The system MUST NOT bypass source access controls or provider policy.

#### Scenario: Import a permitted source candidate
- **WHEN** a configured provider returns a valid candidate with source identity and canonical URL
- **THEN** the system persists one source item with provenance and an observable intake state

#### Scenario: Re-import a candidate
- **WHEN** the same provider identity is submitted again for the same project
- **THEN** the system returns the existing item and does not create a second evidence record

#### Scenario: Provider denies access
- **WHEN** the source provider reports unauthorized, policy-denied, or rate-limited access
- **THEN** the system records the classified attempt, retains prior evidence, and does not bypass the denial

### Requirement: AI screening and deep analysis are typed and staged

The system MUST screen candidates before deep analysis and MUST validate every AI response against a versioned JSON schema and numeric bounds before persistence. BYOK secrets MUST remain outside project/evidence data and logs. AI provider failure MUST leave source evidence intact and expose retry status.

#### Scenario: Screen and analyze a relevant candidate
- **WHEN** a candidate passes screening and deep analysis returns schema-valid output
- **THEN** the system persists versioned typed analysis linked to the source and identifies provider/model metadata

#### Scenario: Reject malformed model output
- **WHEN** an AI response is invalid JSON, violates the schema, or contains out-of-range scores
- **THEN** the system rejects the analysis, records redacted failure metadata, and keeps the candidate available for retry

#### Scenario: Provider timeout or quota exhaustion
- **WHEN** an AI request times out or is rejected by provider quota
- **THEN** the job enters a visible retryable or exhausted state and source evidence remains unchanged

### Requirement: Pain clusters remain evidence-linked and human-correctable

Every cluster summary, assignment, count, and representative item MUST link to retained source evidence. Owner corrections MUST be audited and MUST take precedence over later AI suggestions until explicitly changed.

#### Scenario: Review a cluster
- **WHEN** the owner opens a cluster's evidence view
- **THEN** each displayed source claim includes its source, canonical URL, timestamps, confidence, and original source item

#### Scenario: Correct an AI assignment
- **WHEN** the owner changes a source assignment or merges/splits clusters
- **THEN** the system records an auditable human revision and does not silently overwrite it with a later model result

### Requirement: Trends are deterministic and use explicit time windows

The system MUST calculate 7-, 30-, and 90-day counts and trend labels from persisted UTC observations in application code. AI MUST NOT provide aggregate counts or trend values. When the previous window has zero observations, percent growth MUST be null and the response MUST identify a new signal rather than return infinity.

#### Scenario: Calculate a non-zero baseline trend
- **WHEN** a cluster has persisted observations in current and previous windows
- **THEN** the response returns deterministic counts, delta, configured label, window and as-of timestamp

#### Scenario: Handle a zero previous window
- **WHEN** the current window has observations and the previous window has none
- **THEN** the response returns `percent_growth: null` and `new_signal: true`

### Requirement: Opportunity signals disclose their evidence and limits

An Opportunity Signal MUST expose evidence volume, deterministic growth, current alternatives/workarounds, confidence, and representative source links. It MUST be labeled as an evidence signal and MUST NOT claim business validation.

#### Scenario: Open an opportunity signal
- **WHEN** the owner opens a signal generated from a pain cluster
- **THEN** the UI shows its evidence, time window, source links, confidence, and the statement that the signal is not business validation

#### Scenario: No supporting evidence remains
- **WHEN** a cluster has no retained source items
- **THEN** the system does not present a positive opportunity signal and explains that evidence is unavailable

## Traceability

| Requirement | Proposal | Design boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| R1 Research projects | Research projects | Domain/Application/API/web; `ResearchProject` | Create, empty brief | D1 | CRUD integration plus validation and no-write assertion |
| R2 Candidate intake | Source provenance | Application `ISourceProvider`; Infrastructure adapter | Import, duplicate, denied | D2 | Adapter contract and DB uniqueness/idempotency tests |
| R3 Staged AI | Screening/deep analysis | Application DTO/schema; Hangfire job | Valid, malformed, timeout/quota | D3–D4 | Schema/range tests and fake-provider retry integration |
| R4 Pain clusters | Evidence board | Domain cluster/assignment; API/web | Evidence view, correction | D5 | Human revision integration and source-link UI test |
| R5 Deterministic trends | Trends | Application query/service | Baseline, zero baseline | D6 | Fixed UTC fixture asserts counts/delta/label |
| R6 Opportunity signals | Opportunity card | Application read model/API/web | With/without evidence | D7 | End-to-end evidence link and disclosure assertions |

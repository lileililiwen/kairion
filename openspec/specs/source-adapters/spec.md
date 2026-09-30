# source-adapters Specification

## Purpose
TBD - created by archiving change additional-source-adapters. Update Purpose after archive.
## Requirements
### Requirement: Providers are independently configured and bounded
The system MUST allow an owner to enable supported source providers per research project with validated limits and MUST keep providers disabled until explicitly configured.

#### Scenario: Configure a provider
- **WHEN** an owner enables a provider with valid endpoint and bounded query/result limits
- **THEN** the project stores non-secret configuration and reports the provider as enabled without returning credentials

#### Scenario: Reject unsafe provider configuration
- **WHEN** configuration contains a non-HTTPS endpoint, an unknown provider key, or limits outside the supported range
- **THEN** the API rejects the update and preserves the prior configuration

### Requirement: Retrieval preserves normalized provenance
Each accepted result MUST retain its provider, original canonical URL, source-native ID when available, retrieval time, optional publication time, and safe run status.

#### Scenario: Retrieve valid candidates
- **WHEN** an enabled provider returns valid candidates
- **THEN** Kairion stores normalized candidates linked to the research project and their original source URLs

#### Scenario: Provider returns duplicate URL
- **WHEN** a later run returns the same project and canonical URL
- **THEN** Kairion updates last-seen provenance without creating a duplicate candidate

### Requirement: Provider failures are isolated and inspectable
The system MUST bound provider work, retain validated partial results, expose safe failure status, and leave other providers and existing evidence usable.

#### Scenario: One provider is rate limited
- **WHEN** a provider returns a rate-limit response with retry timing
- **THEN** Kairion records a retryable status and retry-after time while unrelated provider results remain available

#### Scenario: Provider returns malformed or oversized data
- **WHEN** the adapter cannot validate a response within configured bounds
- **THEN** it records a safe failure code and stores neither credentials nor raw response content


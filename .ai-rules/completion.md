# Kairion completion rules

An OpenSpec requirement is complete only when observable success, failure, and applicable boundary scenarios have evidence; callers and persistence migrations are reviewed; local project verification and the shared governance Gate pass; strict OpenSpec validation passes; the change is archived; and its two-commit lifecycle is complete.

AI analysis must use typed, schema-validated outputs. Provider errors, timeouts, malformed output, quota limits, and retries must be visible and must not erase source evidence. Source links and provenance must remain attached to every displayed conclusion. Deterministic counts and trends must be computed from persisted timestamps by application code. Opportunity Signals must be labeled as research evidence, not business validation.

Never claim build, test, runtime, screenshot, release, or deployment evidence that was not observed. Incomplete or blocked work remains incomplete, with the exact failed command and next action in `HANDOFF.md`.

Each completed OpenSpec spec/change requires exactly two commits: first the implementation/tests/archive commit, then a HANDOFF.md-only pointer/evidence commit.

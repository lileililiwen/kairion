# Kairion change workflow

Use OpenSpec for every non-trivial change. Follow BFS → DFS → BFS:

1. **BFS baseline:** inspect source/docs/tests/config/Git state and active OpenSpec work; map affected capabilities, data, contracts, users, dependencies, privacy, security, and failure boundaries before changing code.
2. **DFS implementation:** implement one coherent requirement at a time with its tests; use the selected active change only. Keep source-provider and AI-provider adapters replaceable. Preserve source provenance and avoid collecting data outside the configured provider's permitted access.
3. **BFS regression:** recheck affected routes, storage, callers, provider failures, retries, authorization, privacy, migration, and cross-requirement behavior.
4. **Verification:** run local formatting/build/tests, strict OpenSpec validation, and applicable Gate before archive. Record exact blocked commands and next actions. Build/test/skeleton success is not runtime evidence.

Do not commit secrets, API keys, private user data, or unsanitized discussion content in fixtures/screenshots. BYOK secrets belong in an external secret store or local environment, never project records or logs. Counts and trend calculations are application-owned and reproducible.

Each completed OpenSpec spec/change requires exactly two commits: first the implementation/tests/archive commit, then a HANDOFF.md-only pointer/evidence commit. Do not ask for conversational authorization or confirmation before either commit when the change is already authorized. After commit 2, stop.

Initialization additionally requires a successful `gh auth status`, a preserved or newly configured `origin`, public repo publication without force-push, applied description/homepage/topics, and verification with `gh repo view` plus `git ls-remote --heads origin main`.

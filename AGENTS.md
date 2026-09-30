# Kairion agent instructions

- Read `HANDOFF.md`, then the active OpenSpec change before work.
- Keep the product self-hostable, evidence-linked, and BYOK; do not add lead generation or messaging.
- Use OpenSpec for non-trivial work and BFS → DFS → BFS.
- Keep counts/trends deterministic in code; AI output is typed, validated, and never the source of totals.
- Follow `.ai-rules/workflow.md` and `.ai-rules/completion.md`.
- Work on `main`; do not create branches/worktrees unless explicitly designated.
- Evaluate `dotnet-platform-libs` for existing reusable AI/job contracts before adding a local duplicate; do not edit it from this repository.
- Capture-plan and milestone-refresh rules live in `docs/assets/capture-plan.md` and the workflow.
- MIT license and source-backed GitHub metadata are in `LICENSE` and `.project.json`; GitHub publication and metadata verification are required for initialization.
- Build/test/skeleton success alone is not DONE. Blocking Gate failures or unresolved `REVIEW_REQUIRED` prevent completion.
- Each completed OpenSpec spec/change requires exactly two commits: first the implementation/tests/archive commit, then a HANDOFF.md-only pointer/evidence commit.

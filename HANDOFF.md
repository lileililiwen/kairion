current_spec: evidence-grounded-demand-research-mvp

# Handoff

## State

Repository bootstrap and planning artifacts are in progress. This is **PLANNING ONLY**: no API, UI, database schema, source provider, AI provider, build, or runtime has been implemented or verified. Git repository initialized on `main`; initial publication and GitHub metadata still need verification.

## Active change

`openspec/changes/evidence-grounded-demand-research-mvp/` is the first implementation package. It follows the dependency order in `ROADMAP.md`. Implement only this package before selecting a follow-up.

## Verification evidence

- `openspec init --tools codex` created the repository OpenSpec structure (`spec-driven`). Codex prompt installation was blocked because the user-level prompt directory is read-only; repository OpenSpec files are available.
- .NET SDKs 8.0.424 and 10.0.400 are installed in the workspace environment. No Kairion solution or React app exists yet.
- GitHub CLI authentication passed outside the sandbox. Initial repository existence check found no prior `lileililiwen/kairion`.
- Published with `gh repo create lileililiwen/kairion --public --source . --remote origin --push`; initial commit `fe62f430af922c8bae9ffe2874489773cdddd843` reached `main`.
- `gh repo edit` set the approved description, homepage `https://github.com/lileililiwen/kairion`, and topics `ai, demand-intelligence, founders, market-research, open-source`.
- The shared Workspace Governance GitHub metadata publisher returned `metadata_verified` with no differences and recorded publication in `.project.json`.
- `gh repo view --json nameWithOwner,description,homepageUrl,visibility,repositoryTopics,url` reported public `lileililiwen/kairion`, the matching description/homepage/topics. `git ls-remote --heads origin main` returned `fe62f430af922c8bae9ffe2874489773cdddd843 refs/heads/main`.
- OpenSpec strict validation passed (1 change, 0 failures). Repository CI now runs bootstrap metadata checks and strict OpenSpec validation; .NET/web builds are conditional on those source manifests existing. No app build is currently available.
- Workspace Governance reported `DISCOVERED_UNREGISTERED` for the two new projects; this is expected under the workspace discovery policy and does not require a registry edit. It also reports unrelated pre-existing missing directory `jenkins-bootstrap`.
- No application tests, runtime, or screenshot has been run. Capture is blocked because there is no application source tree; `docs/assets/capture-plan.md` records the next action.

## Next actions

1. Finish and strictly validate the active OpenSpec package.
2. Complete product UI/API implementation and run the declared checks before claiming delivery.
3. Run the app and create a real, privacy-reviewed screenshot.
4. Create/push the public GitHub repository, apply description/homepage/topics, then verify `gh repo view` and `git ls-remote`.

Each completed OpenSpec spec/change requires exactly two commits: first the implementation/tests/archive commit, then a HANDOFF.md-only pointer/evidence commit.

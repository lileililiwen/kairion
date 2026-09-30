#!/usr/bin/env python3
"""Check repository bootstrap metadata and the active OpenSpec package."""

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
errors: list[str] = []

for relative in (
    "README.md", "ROADMAP.md", "HANDOFF.md", "AGENTS.md", "LICENSE",
    ".ai-rules/workflow.md", ".ai-rules/completion.md",
    "docs/assets/capture-plan.md",
):
    if not (ROOT / relative).is_file():
        errors.append(f"missing required file: {relative}")

try:
    project = json.loads((ROOT / ".project.json").read_text(encoding="utf-8"))
except (OSError, json.JSONDecodeError) as exc:
    errors.append(f"invalid .project.json: {exc}")
    project = {}

expected_id = ROOT.name
if project.get("id") != expected_id:
    errors.append(f"project id must match directory name {expected_id!r}")
metadata = project.get("discoverability", {})
for key in ("description", "keywords", "topics", "audience", "use_cases", "search_phrases", "homepage", "license_spdx", "visibility"):
    if not metadata.get(key):
        errors.append(f"discoverability.{key} is required")
if any(not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,49}", topic) for topic in metadata.get("topics", [])):
    errors.append("discoverability topics must be normalized lowercase GitHub topics")
if project.get("deployment", {}).get("deployable") is not False:
    errors.append("deployment.deployable must remain false until release evidence exists")
if project.get("publication", {}).get("state") != "metadata_verified":
    errors.append("GitHub publication must be metadata_verified")

handoff = (ROOT / "HANDOFF.md").read_text(encoding="utf-8") if (ROOT / "HANDOFF.md").exists() else ""
match = re.search(r"^current_spec: ([a-z][a-z0-9]*(?:-[a-z0-9]+)*)$", handoff, re.MULTILINE)
if not match:
    errors.append("HANDOFF.md must contain exactly one canonical current_spec")
else:
    spec_name = match.group(1)
    change = ROOT / "openspec" / "changes" / spec_name
    archive_root = ROOT / "openspec" / "changes" / "archive"
    archived = sorted(archive_root.glob(f"*-{spec_name}")) if archive_root.is_dir() else []
    if change.is_dir():
        # An active (not yet archived) package must be structurally complete.
        for relative in ("proposal.md", "design.md", "tasks.md"):
            if not (change / relative).is_file():
                errors.append(f"active OpenSpec package is missing {relative}")
        spec_dir = change / "specs"
        if not spec_dir.is_dir() or not list(spec_dir.rglob("spec.md")):
            errors.append("active OpenSpec package is missing a capability spec")
    elif archived:
        # The current spec has been completed and archived; require the archive to
        # still carry its capability spec so the record stays complete.
        if not any(p.rglob("specs/*/spec.md") for p in archived):
            errors.append(f"archived OpenSpec package {spec_name!r} is missing its capability spec")
    else:
        errors.append(f"current_spec {spec_name!r} matches neither an active nor an archived OpenSpec package")

# The main capability specs (updated by archive) must remain valid on their own.
specs_root = ROOT / "openspec" / "specs"
if not specs_root.is_dir() or not list(specs_root.rglob("spec.md")):
    errors.append("openspec/specs is missing a capability spec")

if errors:
    for error in errors:
        print(f"ERROR: {error}")
    raise SystemExit(1)
print(f"Repository metadata and OpenSpec state are consistent for {expected_id}.")

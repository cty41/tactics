---
id: wrong-project-root
status: verified
signature: "Build succeeds but Tactics Tooling or Poison Spear assets are absent because the editor opened a different project root"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: editor
language: mixed
last_verified: 2026-08-08
---

# Wrong Godot project root

## Observed

The repository root and an earlier Spike project were both treated as possible Godot roots. C# could build while the editor instance did not expose the expected dock/content.

## Reproduction

Open a directory other than the parent of canonical `godot/project.godot`, or connect automation to an editor instance for another project.

## Cause and resolution

The migration now has exactly one tracked `project.godot` at `godot/project.godot`. Scripts and Skills resolve the canonical path explicitly and fail before running if it is absent.

## Evidence

- `verified_local`: repository search finds one tracked `project.godot`; unified headless commands target its parent directory.
- `verified_local`: the user reopened the canonical project and completed tooling verification.

## Scope and invalidation

Re-run the uniqueness check whenever project layout changes. A copied untracked project outside the repository remains external state and is not authoritative.

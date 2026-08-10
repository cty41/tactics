---
name: godot-ai-workflow
description: Use when configuring or operating the canonical Godot project through the pinned project-scoped godot-ai v3.1.2 Attach server for scene, resource, run, log, screenshot, or lightweight editor smoke tasks.
---

# Godot AI Workflow

## Quick Reference

- Baseline: local `D:/codes/godot-ai`, tag `v3.1.2`, pinned by `Tools/migration/manifest/godot-tooling.json`.
- Project: `godot/project.godot` only.
- Config: local ignored `.codex/config.toml`; never leave godot-ai in the user-level Codex config.
- Profiles: `phase3-observe`, `content-authoring`, `ui-input`, `presentation`; each is cumulative.
- Sync: `Tools/migration/Sync-GodotAiCodexConfig.ps1` imports, validates and switches the exact allowlist.
- Custom Tactics tools remain deferred until the C# mutation kernel is stable.

## When to use

Use for validated, repeatable Godot Editor/MCP operations after Core/Application boundaries and tests are already established.

## Workflow

1. Verify the pin, ports and selected profile in the manifest; never mutate vendored godot-ai source.
2. For first setup, let Godot generate the Windows Codex block once, then run `Sync-GodotAiCodexConfig.ps1 -ImportFromUser -Profile phase3-observe`. Restart a Codex task rooted at the migration worktree.
3. Before every write sequence, call `session_manage` and `editor_state`. Require exactly one session, Godot 4.7.1 and the canonical project path; otherwise stop.
   Repository writes that require session count `0` must use `godot-editor-lifecycle` to close and conditionally restore the verified Editor; do not improvise process commands.
4. Use only tools exposed by the selected cumulative profile. See `references/tool-boundary.md` for the stage matrix and permanent deny-list.
5. Keep domain validation in C# services/CLI/tests. Run GdUnit and headless verification independently of godot-ai smoke.
6. After changing version, ports or profile, rerun sync/check and restart the Codex task. Stop and investigate any project-root or launch drift.

## Examples

- `phase3-observe`: read the catalog, run the project, collect logs/screenshots and stop it.
- `content-authoring`: create repetitive Node structure only after the Scene/Resource contract and tests exist.

## Anti-patterns

- Do not make godot-ai a Core, Application, runtime, catalog or migration-ledger dependency.
- Do not use generic MCP composition as proof of atomic domain mutation.
- Do not edit the pinned plugin to solve a project-specific workflow.
- Do not use `script_*`, `filesystem_manage`, `client_manage` or `autoload_manage`.

## Checklist

- [ ] Pin, project-local config and active profile verified.
- [ ] Exactly one canonical Session and Editor root verified before writes.
- [ ] Generic operation is within the profile allowlist.
- [ ] Domain/test truth remains outside godot-ai.
- [ ] Independent tests and logs passed.

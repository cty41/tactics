---
id: csharp-assembly-reload-duplicate-type
status: verified
signature: "System.ArgumentException: An item with the same key has already been added in ScriptTypeBiMap.Add"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: editor
language: csharp
last_verified: 2026-08-08
---

# C# assembly reload duplicate type registration

## Observed

After Build/Reload, the editor reported a duplicate key for `TacticsGraphWorkbench+MarkerState`, followed by repeated `.NET: Failed to unload assemblies` and “Giving up on assembly reloading”. The tooling dock disappeared until restart/fix.

## Reproduction

The exact old nested `MarkerState` implementation is no longer present. The original editor log is retained in task evidence; the normalized signature routes future recurrences here.

## Cause and resolution

For this project occurrence, a reloadable tool assembly exposed a duplicate nested script type registration and unloading failed. The nested registered type was removed, editor-owned objects/signals gained symmetrical cleanup, the editor was restarted, and the user verified C# Reload, GraphEdit, Undo/Redo and SubViewport checks.

This is a project-scoped resolution, not proof that all Godot assembly unload failures share the same cause.

## Evidence

- `verified_local`: manual 4.7.1 editor verification passed after the code change and restart.
- `upstream_open`: https://github.com/godotengine/godot/issues/78513 — open tracker, queried 2026-08-09; documents that unload can fail for multiple reasons and scripts remain unavailable until restart.
- `upstream_open`: https://github.com/godotengine/godot-proposals/issues/9001 — open proposal, queried 2026-08-09; describes C# tool-script reload state, constructor/property side effects and non-deterministic peer reload order.

## Scope and invalidation

Applies to the prior `MarkerState` failure on Godot 4.7.1 C# Editor. Re-open investigation if the signature returns without that nested type or after Godot upgrades.

---
id: script-mainloop-entry
status: verified
signature: "Can't load the script as it doesn't inherit from SceneTree or MainLoop"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: headless
language: csharp
last_verified: 2026-08-08
---

# `--script` C# entry must be a MainLoop

## Observed

Godot `--script` refused the original C# asset builder because it was a plain helper/editor class rather than a `MainLoop` implementation.

## Reproduction

Run a C# script that does not derive from `SceneTree` or `MainLoop` through `Godot --headless --script <path>`.

## Cause and resolution

The command-line script is the engine main loop. `PoisonSpearAssetBuilder` now derives from `SceneTree`, performs work in `_Initialize`, then calls `Quit`. EditorPlugin command routing is also available for editor-hosted generation.

## Evidence

- `verified_local`: `godot/src/Tactics.Godot.Adapter/Editor/PoisonSpearAssetBuilder.cs` runs under local 4.7.1 Mono.
- `official_docs`: Godot command-line `--script` contract and MainLoop/SceneTree class model; queried 2026-08-08 for 4.7.

## Scope and invalidation

Applies to command-line `--script`, not arbitrary EditorPlugin callbacks. Recheck after an engine major/minor upgrade.

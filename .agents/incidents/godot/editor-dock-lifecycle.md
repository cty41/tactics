---
id: editor-dock-lifecycle
status: verified
signature: "Tactics Tooling dock appears briefly and closes during plugin initialization or reload"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: editor
language: csharp
last_verified: 2026-08-08
---

# Editor surface lifecycle cleanup

## Observed

The Tactics Tooling surface appeared briefly during project reload and then closed. In the recorded occurrence, assembly unloading errors prevented a stable plugin lifecycle.

## Reproduction

Open/reload the canonical project while the tool assembly cannot finish reload, or throw during dock initialization after partial registration.

## Cause and resolution

The original fix created and registered one `EditorDock` inside `_EnterTree`, caught partial initialization failures, and used one idempotent cleanup path from both the exception handler and `_ExitTree`. The current implementation supersedes that bottom dock with a supported main-screen plugin: `_EnterTree` adds one workbench to `EditorInterface.get_editor_main_screen()`, `_MakeVisible` owns workspace switching, and `_ExitTree` queues the workbench for deletion. The workbench still disconnects button signals and releases preview references in its own `_ExitTree`.

## Evidence

- `official_docs`: https://docs.godotengine.org/en/4.7/tutorials/plugins/editor/making_main_screen_plugins.html — main-screen controls are added to the editor main screen, hidden initially, switched through `_MakeVisible`, and freed on exit; queried 2026-08-09.
- `verified_local`: the central-workspace implementation builds cleanly, survives a follow-up assembly reload without new editor errors, and a fresh headless editor initialization exits cleanly. Manual central-workspace visual acceptance remains pending.

## Scope and invalidation

Cleanup prevents leaked/duplicate editor objects; it cannot by itself repair an assembly unload failure. Revalidate when changing the main-screen API or plugin ownership.

---
id: export-release-editor-dependency-graph-contamination
status: verified
signature: "CS0246 EditorPlugin or EditorUndoRedoManager missing after ExportRelease; typed C# Resource loads as Godot.Resource"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.316
os: Windows
context: build
language: csharp
last_verified: 2026-08-16
---

# ExportRelease contaminates the Editor dependency graph

## Observed

After reopening the canonical project, five Content Workbench loads could not cast `GodotResourceCatalog` and one Map load could not cast `PureRunMapResource`; the loaded objects reported the base `Godot.Resource` type. A build against the same intermediate graph also reported missing editor-only types such as `EditorPlugin` and `EditorUndoRedoManager`.

## Reproduction

Restore the Debug/Editor graph and record the SHA-256 of `godot/.godot/mono/temp/obj/project.assets.json`. Publish the Adapter as `ExportRelease` with `GodotTargetPlatform=windows` while leaving `GodotProjectDir` pointed at the canonical project. The shared assets file is replaced by the export graph and no longer contains `GodotSharpEditor/4.7.1`; restarting the Editor then reproduces the tool-script and typed Resource failures. Passing only `--artifacts-path` does not isolate the Godot SDK intermediate path.

## Cause and resolution

Godot.NET.Sdk derives its intermediate dependency graph from `GodotProjectDir/.godot/mono/temp`, so an in-place ExportRelease publish overwrote the graph used by the Editor. The Windows build now supplies both a temporary `GodotProjectDir` and an isolated `--artifacts-path`, verifies the canonical assets hash is unchanged, and deletes the temporary artifacts in `finally`. The unified verifier restores the Editor graph and refuses to continue unless it contains `GodotSharpEditor/4.7.1`. Workbenches additionally use a bounded deferred script/schema probe before typed casts; runtime fixture loads remain fail-fast with explicit path, type, and script diagnostics.

## Evidence

- `reproduced_local`: an unisolated ExportRelease publish changed the canonical assets graph and removed `GodotSharpEditor/4.7.1`; `--artifacts-path` alone did not prevent it.
- `verified_local`: the isolated publish produced the ExportRelease assembly while the canonical assets SHA-256 remained identical before and after.
- `verified_local`: `Verify-GodotMigration.ps1` passed on 2026-08-16, including 103 Core, 114 Application, 15 Unity Oracle, 15 Gameplay Spec journeys, three new reload-safe Resource tests, headless Editor/plugin checks, both renderer validations, 148 migration Python tests, and 16 OKF tests.

## Scope and invalidation

This record applies to Godot 4.7.1 Mono on Windows when the same checkout is used for Editor and ExportRelease builds. Revalidate the intermediate-path behavior after a Godot.NET.Sdk upgrade or build-layout change. A real interactive C# Assembly Reload and workbench layout/signal check remains tracked by `MQA-GODOT-CONTENT-WORKBENCH` and `MQA-GODOT-RELOAD-OUTPUT` rather than being inferred from automated evidence.

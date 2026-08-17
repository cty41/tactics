---
id: typed-resource-reload
status: verified
signature: "C# typed Resource is unavailable immediately after tool assembly reload"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: editor
language: csharp
last_verified: 2026-08-17
---

# Typed Resource, authoring bridge and C# reload boundary

## Observed

The editor workbench could initialize while a C# Resource script was temporarily unavailable during reload. Native Presentation first reproduced this as an `InvalidCastException`: `ResourceLoader.Load<UnitDefinitionResource>` received a base `Godot.Resource` even though the catalog path and serialized script were valid. A later real Reload also failed to unload the old assembly after an MCP preview request.

## Reproduction

Build/reload the C# tool assembly while the editor reconstructs tool scripts and immediately initialize a dock that assumes every custom Resource type is already rebound.

## Cause and resolution

Editor initialization now has one readiness probe for every Resource type used immediately by the authoring surface. It distinguishes reload-pending types from real script/schema drift and keeps the Main Screen and bridge fail-closed. Nested editor dependencies use `CacheMode.IgnoreDeep`; the Preview creates a tool-safe `GodotUnitActor` shell and still invokes the production actor configuration/player semantics instead of expecting a non-`[Tool]` PackedScene root to instantiate as its runtime C# type.

A .NET dump identified the unload root as the process-wide `System.Text.Json` reflection-emission cache retaining types from Godot's collectible project assembly. The live authoring protocol, workspace batch, ownership ledger and Skill event evidence therefore use explicit `JsonObject`/`Utf8JsonWriter` boundaries rather than reflection serialization of project types. The NamedPipe server owns its active pipe, rejects new work before reload, cancels and disposes the pipe, drains pending requests with a bounded synchronous wait, then removes its descriptor. Reload recovery is deferred until Godot has restored script instances; the new bridge always rotates pipe and token. Dead-PID descriptors are ignored by MCP resolution.

## Evidence

- `verified_local`: reload-safe Resource tests 7/7 and the complete `Tools/godot/Verify-GodotProject.ps1` gate passed on Godot 4.7.1 Mono and .NET 9 on 2026-08-17.
- `verified_real_reload`: `Tools/godot/Test-TacticsToolingAssemblyReload.ps1` performed cold Skill/Status/Unit previews, one real same-PID C# Reload with pipe/token rotation, a post-reload native preview and clean exit. The captured log contained no Godot `ERROR`, `InvalidCastException`, assembly unload, stale delegate or missing-method signature.
- `manual_boundary`: SubViewport appearance, Graph readability and Apply/Undo/Redo interaction feel remain pending; automation does not mark them visually accepted.
- `upstream_open`: https://github.com/godotengine/godot-proposals/issues/9001 — peer reload order and post-deserialization readiness remain an open design area; queried 2026-08-09.

## Scope and invalidation

This workaround is limited to editor initialization during reload. It does not prohibit typed Resources at stable runtime and must not erase schema/type diagnostics.

If the failure persists across a stable editor restart and `GodotSharpEditor` is absent from the Editor dependency graph, route to [export-release-editor-dependency-graph-contamination](export-release-editor-dependency-graph-contamination.md) instead of treating it as a transient reload boundary.

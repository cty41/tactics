---
id: editor-resource-missing-tool
status: verified
signature: "Unable to cast object of type Godot.Resource to a custom GlobalClass Resource inside EditorPlugin"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.316
os: Windows
context: editor
language: csharp
last_verified: 2026-08-16
---

# Editor-loaded C# Resource requires the Tool contract

## Observed

After the Editor dependency graph was restored, Content Workbench catalog/map probes and their nested Event, Treasure, AI, Encounter, and Layout previews still loaded the correct serialized script path and properties but instantiated as the base `Godot.Resource`. The same typed loads passed in runtime and headless GdUnit processes.

## Reproduction

Define a C# `Resource` with `[GlobalClass]` but without `[Tool]`, save a `.tres` using that script, and load it from an active C# `EditorPlugin` workbench. The serialized script remains visible, but a typed cast fails in Editor context. Adding `[Tool]`, rebuilding with the Editor closed, and reopening the same canonical project restores typed instantiation.

## Cause and resolution

`[GlobalClass]` registers the custom type, while `[Tool]` is the contract that permits the script to execute in Editor context. The Resource classes directly loaded by Content Workbench now carry both attributes: catalog, catalog entry, map, event, treasure, AI, encounter, and battle layout. The workbench loader still checks exact script/schema and uses cache-ignore loads so malformed content is not hidden as a reload delay.

The Map and AI GraphEdit rebuilds also remove only authored `GraphNode` children. Removing every child deletes Godot's non-internal connection layer and produces repeated `connections_layer is missing` errors.

## Evidence

- `official_docs`: Godot 4.7 custom Resource and editor-plugin documentation distinguishes `[GlobalClass]` registration from optional `[Tool]` editor execution; queried through Context7 on 2026-08-16.
- `verified_local`: after the scoped attributes and GraphNode-only cleanup, the canonical 4.7.1 Editor reached ready with Main.tscn open and reported zero Editor errors/warnings through MCP.
- `verified_local`: MCP started Main.tscn, the helper reached `live`, logged `Tactics Godot playable run UI ready`, and both Editor and game error buffers remained empty.

## Scope and invalidation

This applies to custom C# Resources instantiated by EditorPlugin code. Runtime-only Resource types do not need `[Tool]`; expand the attribute set only when a type enters an Editor authoring/preview dependency closure. Revalidate after a Godot C# tool-mode lifecycle change.

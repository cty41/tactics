---
id: csharp-assembly-reload-field-type-mismatch
status: verified
signature: "System.InvalidCastException: Unable to cast object of type 'Godot.VSplitContainer' to type 'Godot.HSplitContainer' in RestoreGodotObjectData"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.316
os: Windows 11 10.0.26200
context: editor
language: csharp
last_verified: 2026-08-09
---

# C# assembly reload field type mismatch

## Observed

While the Tactics Tooling workbench was live, its split field changed directly from `VSplitContainer` to `HSplitContainer`. The next C# Build/Reload failed while restoring the old tool instance because the generated deserializer tried to cast the retained vertical split node to the new horizontal split field type.

## Reproduction

1. Keep a `[Tool]` C# node alive in the Godot 4.7.1 Editor with a private `VSplitContainer` field referencing a live child.
2. Change the same field to `HSplitContainer` without restarting the Editor.
3. Build the C# project and let Godot restore the tool object's state.
4. `RestoreGodotObjectData` throws the normalized `InvalidCastException` above.

## Cause and resolution

Godot's generated C# reload serializer restores the prior field value by field identity. The live value remains a `VSplitContainer`, but the newly generated field deserializer requires `HSplitContainer` and performs an invalid cast.

The field now uses their common `SplitContainer` base type while fresh plugin initialization still constructs an `HSplitContainer`. A second Build/Reload produced no new editor errors, and a fresh headless Editor initialization exited successfully. This is a compatibility measure for this project occurrence, not a claim that every C# reload field change requires a base type.

## Evidence

- `verified_local`: Godot 4.7.1 Editor captured the exact generated `RestoreGodotObjectData` stack, followed by a clean Build/Reload after the base-type change.
- `verified_local`: `dotnet build godot/Tactics.Godot.Adapter.csproj --no-restore --configuration Debug` passed with 0 warnings and 0 errors on .NET SDK 9.0.316.
- `verified_local`: a fresh headless Editor initialization of the canonical project exited with code 0.

## Scope and invalidation

Applies to live C# tool objects whose serialized Godot-object field changes to an incompatible sibling type during assembly reload. It does not apply when the Editor is fully restarted before loading the new assembly. Revalidate after upgrading Godot or changing C# reload serialization behavior.

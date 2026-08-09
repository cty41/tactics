---
id: typed-resource-reload
status: verified
signature: "C# typed Resource is unavailable immediately after tool assembly reload"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: editor
language: csharp
last_verified: 2026-08-08
---

# Typed Resource and C# reload boundary

## Observed

The editor workbench could initialize while the C# `PoisonSpearPresentationResource` type was temporarily unavailable during reload, making a direct typed load brittle.

## Reproduction

Build/reload the C# tool assembly while the editor reconstructs tool scripts and immediately initialize a dock that assumes every custom Resource type is already rebound.

## Cause and resolution

Workbench loading now uses untyped `ResourceLoader.Load`, checks for null, and reads the minimal serialized properties through Godot Variant APIs. Runtime validation still performs typed loads after the assembly is stable. Initialization is deferred and guarded against duplicate execution.

## Evidence

- `verified_local`: manual C# Reload and tooling checklist passed on local 4.7.1.
- `upstream_open`: https://github.com/godotengine/godot-proposals/issues/9001 — peer reload order and post-deserialization readiness remain an open design area; queried 2026-08-09.

## Scope and invalidation

This workaround is limited to editor initialization during reload. It does not prohibit typed Resources at stable runtime and must not erase schema/type diagnostics.

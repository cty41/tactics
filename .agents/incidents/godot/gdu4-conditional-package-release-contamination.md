---
id: gdu4-conditional-package-release-contamination
status: verified
signature: "Release deps.json contains gdUnit4 and Microsoft.TestPlatform after Debug conditional restore"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: build
language: csharp
last_verified: 2026-08-09
---

# GdUnit conditional package Release contamination

## Observed

`Tactics.Godot.Adapter.csproj` originally enabled GdUnit sources and packages only when `Configuration=Debug`. A later `Release --no-restore` build still copied GdUnit/TestPlatform assemblies and listed them in `Tactics.Godot.Adapter.deps.json`.

## Reproduction

Restore the conditional project with the default Debug configuration, run its GdUnit tests, then build Release with `--no-restore`. Inspect the fresh Release output and `.deps.json` for `gdUnit4`, `Microsoft.TestPlatform` or `testhost`.

## Cause and resolution

NuGet restore produced one shared `project.assets.json` while the package graph depended on `Configuration`. Release reused the Debug-restored graph. The production csproj now has no test packages. `Tactics.Godot.TestHost.csproj` uses the assembly name required by `project.godot`, but has a separate intermediate directory, lock file, sources and packages. Because both projects must emit the same configured Debug assembly name, the verifier builds the test host non-incrementally, runs GdUnit, then restores the production Debug assembly non-incrementally. A fresh production Release output is checked for both forbidden assemblies and dependency entries.

## Evidence

- `verified_local`: the contaminated Release contained GdUnit/TestPlatform assemblies and matching `.deps.json` entries.
- `official_docs`: GdUnit4Net documents adding `gdUnit4.test.adapter` to a test project; it does not document Configuration-conditional package isolation inside the production project.
- `verified_local`: the isolated test host passed all three GdUnit tests, while a fresh production Release reported zero forbidden assemblies and zero forbidden `.deps.json` entries.
- `verified_local`: a normal incremental test-host build could retain a newer production DLL and discover zero tests; the explicit non-incremental test-host/production sequence passed repeatedly.

## Scope and invalidation

Applies to the current NuGet/MSBuild layout and GdUnit4Net 3.1.1. Re-evaluate if GdUnit gains an officially supported external runtime-test assembly model or the project adopts configuration-isolated NuGet restore assets by design.

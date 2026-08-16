---
id: parallel-build-obj-contention
status: verified
signature: "Parallel Core and Godot builds contend for Tactics.Core obj or output files"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: build
language: csharp
last_verified: 2026-08-09
---

# Parallel build `obj` contention

## Observed

Running Core NUnit and Godot/GdUnit builds concurrently caused intermittent access/copy failures around shared `src/Tactics.Core/obj` and `Tactics.Core.dll` outputs. Each command passed when run sequentially.

## Reproduction

Start separate `dotnet test` processes for Core and the Godot test project at the same time; both transitively build `Tactics.Core` into the same intermediate/output paths.

## Cause and resolution

Independent MSBuild processes race over one referenced project's intermediate files. The authoritative verifier restores/builds once with one MSBuild node and executes all tests sequentially with `--no-build`.

## Evidence

- `verified_local`: parallel commands failed on the shared output while sequential Core, GdUnit and full solution build passed.
- `verified_local`: `Tactics.Migration.runsettings` limits test concurrency and the unified verifier is ordered.

## Scope and invalidation

Applies while projects share the current Core intermediate/output directories. Isolated `BaseIntermediateOutputPath` could permit safe parallelism later, but must be proven before changing the rule.

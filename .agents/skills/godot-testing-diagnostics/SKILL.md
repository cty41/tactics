---
name: godot-testing-diagnostics
description: Use when building or testing Tactics Core, Application, Godot C#, GdUnit4Net, headless runtime/editor, migration tools, skills, Incidents, or diagnosing engine errors.
---

# Godot Testing Diagnostics

## Quick Reference

Run `Tools/migration/Verify-GodotMigration.ps1` from repository root. It is deliberately sequential because Core and Godot builds previously contended for shared `obj` outputs.

## When to use

Use after migration code/tool/policy changes or whenever build, GdUnit, headless, plugin, reload or resource validation fails.

## Workflow

1. Capture the first exact error signature, command, context and version.
2. Reproduce with the narrowest applicable step; do not hide it with a full clean.
3. If it is engine/version-sensitive, route through Research Guide.
4. Fix the cause and run the narrow test again.
5. Run the unified sequential verifier: restore locked dependencies, build, Core NUnit, Application NUnit, Python tests, GdUnit, runtime/editor headless, policy lint and OKF.
6. Record an engine/toolchain issue as an Incident with local evidence; ordinary syntax mistakes stay in normal task history.

## Examples

- File-lock errors mentioning `Tactics.Core/obj` route to the parallel-build Incident.
- `ScriptTypeBiMap.Add` duplicate keys route to the C# assembly-reload Incident.

## Anti-patterns

- Do not run Core and Godot `dotnet test` in parallel.
- Do not delete `.godot` or all build outputs before preserving the first failure signature.
- Do not treat `Build succeeded` as GdUnit/editor/runtime validation.

## Checklist

- [ ] Exact signature/context/version captured.
- [ ] Narrow reproduction performed.
- [ ] Unified sequential verifier passed.
- [ ] Relevant Incident updated only when evidence warrants it.

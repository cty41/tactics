---
name: godot-workflow
description: Use when a task changes, diagnoses, tests, researches, or migrates the canonical Godot 4.7 C# project; routes work to the smallest specialist skill and evidence set.
---

# Godot Workflow

## Quick Reference

| Need | Load |
|---|---|
| C# Node/Resource/runtime code | `godot-csharp-development` |
| Unity asset export or Godot generation | `godot-content-migration` |
| EditorPlugin/GraphEdit/Undo/preview | `godot-editor-tooling` |
| Build, NUnit, GdUnit or engine error | `godot-testing-diagnostics` |
| godot-ai editor operation | `godot-ai-workflow` |
| Close Editor for exclusive work and restore it | `godot-editor-lifecycle` |
| Unknown/version-sensitive behavior | `references/research-guide.md` |

Canonical project: `godot/project.godot`. Do not create another project or worktree.

## When to use

Use for every Godot migration task when the correct project boundary, specialist workflow, test layer, or evidence level is not already explicit.

## Workflow

1. Confirm the worktree is `migration/godot` and the project is `godot/project.godot`.
2. Read `.agents/knowledge/operations/godot-agent-workflow.md` and the task-relevant migration concept.
3. Select the smallest specialist skill from the table; load multiple only when the task truly crosses boundaries.
4. If the authorized work requires Editor session count `0`, route through `godot-editor-lifecycle`; restore only an Editor that workflow closed.
5. If an API, lifecycle, version, plugin, or engine error is uncertain, follow `references/research-guide.md` before changing code.
6. Implement without adding Unity/Godot references to Core/Application or dev-tool dependencies to release runtime.
7. Run `Tools/godot/Verify-GodotProject.ps1`; narrow diagnostics may run first, but the unified gate is authoritative.
8. Record a new engine pitfall as an Incident. Promote only verified conclusions to OKF and only repeated workflow changes to a Skill.

## Examples

- “C# Resource reload 后类型失效”：load C# development, editor tooling, testing diagnostics, then Research Guide.
- “迁移一个 Buff asset”：load content migration; add editor tooling only if an editor surface changes.
- “读取场景并运行日志 smoke”：load godot-ai workflow; do not create a custom Tactics tool yet.

## Anti-patterns

- Do not answer version-sensitive Godot questions from model memory.
- Do not scan all Incidents; route by normalized error signature or subsystem.
- Do not treat the Poison Spear Spike as parity evidence.
- Do not create a second `project.godot` to simplify tests.

## Checklist

- [ ] Canonical project/worktree confirmed.
- [ ] Specialist Skill and relevant OKF page loaded.
- [ ] Unknown behavior researched with evidence labels.
- [ ] Required build/tests passed sequentially.
- [ ] Incident/OKF/Skill promotion boundary respected.

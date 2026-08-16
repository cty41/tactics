---
name: godot-editor-tooling
description: Use when changing Godot C# EditorPlugin, EditorDock, GraphEdit, Inspector, UndoRedo, SubViewport preview, Resource save, or assembly reload behavior.
---

# Godot Editor Tooling

## Quick Reference

- `[Tool]` plus `#if TOOLS` for editor-only code.
- Add UI in `_EnterTree`; remove and free it in `_ExitTree`.
- One user mutation maps to one typed ChangeSet and one Undo action.
- Preview consumes the same plan semantics as runtime but owns separate transient objects.

## When to use

Use for editor docks, graph/inspector editing, Undo/Redo, preview viewports, saving and C# assembly/resource reload issues.

## Workflow

1. Define the application service/ChangeSet boundary before editor widgets mutate content.
2. Make `_EnterTree` re-entrant and exception-safe; avoid duplicate docks/signals.
3. Register Undo do/undo methods before committing one atomic action.
4. Stage and validate saves before replacing production resources.
5. On `_ExitTree`, disconnect signals, cancel preview work, remove the dock, free SubViewport/transient nodes and clear references.
6. Build, trigger reload, reopen the dock, verify GraphEdit action, Ctrl+Z/Ctrl+Y and SubViewport manually when required.
7. For reload failures, follow Research Guide and record a scoped Incident instead of a universal workaround.

## Examples

- Assembly reload evidence records the exact type, editor context and Godot 4.7.1 version.
- A graph marker Add operation is one Undo action whose redo reproduces the same stable node ID.

## Anti-patterns

- Do not retain delegates, Nodes or Resources in static fields across reload.
- Do not let preview mutate gameplay state.
- Do not claim headless initialization proves visual behavior.

## Checklist

- [ ] Application mutation boundary exists.
- [ ] Exit/reload/exception cleanup is symmetrical.
- [ ] Undo is atomic and stable-ID based.
- [ ] Headless tests passed; manual reload/visual gate recorded when applicable.

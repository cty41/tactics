# Plan: Expand unity-asset-inspection.md

## Goal
Expand `unity-asset-inspection.md` and merge `unity-prefab-editing.md` into a comprehensive **Unity MCP Agent Operation Rules** document covering both inspection and modification operations.

## New Document Structure

| Chapter | Lines | Content |
|---------|-------|---------|
| 1. Core Rules | ~30 | Foundational principles: no YAML, tools-first, open/close pairing |
| 2. Tool Category Index | ~100 | All 57 MCP tools organized by function |
| 3. Workflow Patterns | ~100 | 7 common workflows at a glance |
| 4. Best Practices | ~50 | Pairing rules, error handling, safety notes |
| 5. Tool Reference | ~150 | Each tool's summary (name, required params, purpose) |

**Estimated total:** ~430 lines

---

## Tool Category Index

| Category | Tools |
|----------|-------|
| **Asset** | assets-find, assets-get-data, assets-modify, assets-refresh, assets-copy, assets-move, assets-delete, assets-create-folder |
| **Prefab** | assets-prefab-open, assets-prefab-save, assets-prefab-close, assets-prefab-create, assets-prefab-instantiate |
| **Scene** | scene-open, scene-save, scene-unload, scene-list-opened, scene-get-data, scene-set-active |
| **GameObject** | gameobject-find, gameobject-create, gameobject-modify, gameobject-destroy, gameobject-duplicate, gameobject-set-parent |
| **Component** | gameobject-component-get, gameobject-component-add, gameobject-component-modify, gameobject-component-destroy, gameobject-component-list-all |
| **Object** | object-get-data, object-modify |
| **Editor** | editor-selection-get, editor-selection-set, editor-application-get-state, editor-application-set-state, console-get-logs, console-clear-logs |

---

## Workflow Patterns (7 types)

### 1. Asset Inspection
```
assets-find → assets-get-data
```

### 2. Prefab Editing
```
assets-prefab-open → gameobject-find → gameobject-component-modify → assets-prefab-save → assets-prefab-close
```

### 3. Scene Operations
```
scene-open → scene-get-data / gameobject-find → modifications → scene-save
```

### 4. Component Management
```
gameobject-find → gameobject-component-get → gameobject-component-add/modify/destroy
```

### 5. GameObject Management
```
gameobject-find → gameobject-create/destroy/duplicate/set-parent
```

### 6. Asset Creation
```
assets-create-folder → gameobject-create → assets-prefab-create
```

### 7. Playmode Control
```
editor-application-get-state → editor-application-set-state → verify
```

---

## Tool Reference Format

```markdown
### gameobject-create
Create a new GameObject  
**Required:** name  
**Optional:** parentGameObjectRef, position, rotation, scale, isLocalSpace, primitiveType
```

---

## Execution Steps

1. **Merge content** - Incorporate `unity-prefab-editing.md` into `unity-asset-inspection.md`
2. **Rewrite Core Rules** - Add open/close pairing principle
3. **Add Tool Category Index** - Organize all tools by category
4. **Add Workflow Patterns** - High-level overview of 7 common flows
5. **Add Best Practices** - Pairing, error handling, safety
6. **Add Tool Reference** - Simplified summary for each tool
7. **Delete unity-prefab-editing.md** - Content merged

---

## Status
- [ ] Merge and expand unity-asset-inspection.md
- [ ] Delete unity-prefab-editing.md
- [ ] Commit changes

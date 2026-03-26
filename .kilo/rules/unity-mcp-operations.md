---
description: "Unity MCP tools for asset inspection and modification"
alwaysApply: true
globs: ["**/*.prefab", "**/*.unity", "**/*.asset"]
---

# Unity MCP Agent Operation Rules

This document covers both **inspection** (reading) and **modification** (writing) of Unity assets via MCP tools.

## Core Rules

### Foundational Principles

1. **NEVER** directly edit YAML text content of `.asset`, `.prefab`, or `.unity` files
2. **ALWAYS** use MCP tools for asset operations
3. **ALWAYS** pair open/close operations (open prefab → close prefab, open scene → close/unload scene)
4. **NEVER** assume tools exist - verify MCP server is configured

### Open/Close Pairing

| Operation | Open | Close |
|-----------|------|-------|
| Prefab | `assets-prefab-open` | `assets-prefab-close` |
| Scene | `scene-open` | `scene-unload` |

Always close what you open. Leaving prefab edit mode without saving discards changes.

### Tool Priority

- Use specialized tools over generic ones (e.g., `gameobject-component-modify` over `object-modify`)
- Use `assets-find` first to locate assets, then `assets-get-data` for details
- Use `gameobject-find` to locate GameObjects in opened prefabs/scenes

---

## Tool Category Index

### Asset Operations
| Tool | Purpose |
|------|---------|
| `assets-find` | Search asset database by name, label, type, or GUID |
| `assets-get-data` | Get serialized fields and properties of an asset |
| `assets-modify` | Modify asset file content |
| `assets-refresh` | Refresh AssetDatabase, trigger recompilation |
| `assets-copy` | Copy assets to new paths |
| `assets-move` | Move/rename assets |
| `assets-delete` | Delete assets |
| `assets-create-folder` | Create folder hierarchy |
| `assets-material-create` | Create material with shader |

### Prefab Operations
| Tool | Purpose |
|------|---------|
| `assets-prefab-open` | Enter prefab edit mode |
| `assets-prefab-save` | Save prefab changes |
| `assets-prefab-close` | Exit prefab edit mode |
| `assets-prefab-create` | Create prefab from GameObject |
| `assets-prefab-instantiate` | Instantiate prefab in scene |

### Scene Operations
| Tool | Purpose |
|------|---------|
| `scene-open` | Open scene (Single or Additive) |
| `scene-save` | Save scene to asset file |
| `scene-unload` | Unload scene from editor |
| `scene-list-opened` | List currently opened scenes |
| `scene-get-data` | Get root GameObjects and scene metadata |
| `scene-set-active` | Set active scene |

### GameObject Operations
| Tool | Purpose |
|------|---------|
| `gameobject-find` | Find GameObject by path, name, or instanceID |
| `gameobject-create` | Create new GameObject |
| `gameobject-modify` | Modify GameObject fields/properties |
| `gameobject-destroy` | Destroy GameObject and children |
| `gameobject-duplicate` | Duplicate GameObjects |
| `gameobject-set-parent` | Set parent relationship |

### Component Operations
| Tool | Purpose |
|------|---------|
| `gameobject-component-get` | Get component details and serialized data |
| `gameobject-component-add` | Add component to GameObject |
| `gameobject-component-modify` | Modify component fields/properties |
| `gameobject-component-destroy` | Remove component from GameObject |
| `gameobject-component-list-all` | List all available component types |

### Object Operations
| Tool | Purpose |
|------|---------|
| `object-get-data` | Get serialized data of any Unity object |
| `object-modify` | Modify any Unity object's fields/properties |

### Editor Operations
| Tool | Purpose |
|------|---------|
| `editor-selection-get` | Get current editor selection |
| `editor-selection-set` | Set editor selection |
| `editor-application-get-state` | Get playmode/compilation state |
| `editor-application-set-state` | Start/stop/pause playmode |
| `console-get-logs` | Retrieve Unity Editor logs |
| `console-clear-logs` | Clear log cache |

### Advanced
| Tool | Purpose |
|------|---------|
| `gameobject-component-list-all` | Search component types |
| `assets-shader-list-all` | List available shaders |
| `assets-find-built-in` | Search built-in Unity assets |

---

## Workflow Patterns

### 1. Asset Inspection
```
assets-find → assets-get-data
```
1. Use `assets-find` to locate asset by name, type, or GUID
2. Use `assets-get-data` to retrieve serialized properties

### 2. Prefab Inspection
```
assets-prefab-open → gameobject-find → assets-prefab-close
```
1. Open prefab with `assets-prefab-open`
2. Find target GameObject with `gameobject-find`
3. Exit prefab mode with `assets-prefab-close`

### 3. Prefab Editing
```
assets-prefab-open → gameobject-find → gameobject-component-modify → assets-prefab-save → assets-prefab-close
```
1. Open prefab with `assets-prefab-open`
2. Locate target with `gameobject-find`
3. Modify component with `gameobject-component-modify`
4. Save changes with `assets-prefab-save`
5. Exit with `assets-prefab-close`

### 4. Scene Inspection
```
scene-open → scene-get-data / gameobject-find → scene-unload
```
1. Open scene with `scene-open`
2. Inspect with `scene-get-data` or `gameobject-find`
3. Unload when done with `scene-unload`

### 5. Component Management
```
gameobject-find → gameobject-component-get → gameobject-component-add/modify/destroy
```
1. Find GameObject with `gameobject-find`
2. Get component info with `gameobject-component-get`
3. Add/modify/destroy as needed

### 6. GameObject Creation
```
gameobject-create → [add components] → assets-prefab-create
```
1. Create empty GameObject with `gameobject-create`
2. Add components with `gameobject-component-add`
3. Optionally save as prefab with `assets-prefab-create`

### 7. Playmode Testing
```
editor-application-get-state → editor-application-set-state → verify → editor-application-set-state
```
1. Check current state with `editor-application-get-state`
2. Start playmode with `editor-application-set-state`
3. Test functionality
4. Stop playmode when done

---

## Asset Type Handling

| Asset Type | Inspection | Modification |
|------------|------------|--------------|
| Material, ScriptableObject, Font, Texture | `assets-get-data` | `assets-modify` |
| Scene (.unity) | `scene-open` → `scene-get-data` | `scene-open` → modify → `scene-save` |
| Prefab (.prefab) | `assets-prefab-open` → `gameobject-find` | `assets-prefab-open` → modify → `assets-prefab-save` |
| GameObject in Scene | `gameobject-find` | `gameobject-find` → modify |

---

## Best Practices

### Pairing Rules

| Always Pair | Reason |
|-------------|--------|
| `assets-prefab-open` → `assets-prefab-close` | Leave prefab edit mode |
| `scene-open` → `scene-unload` | Free editor memory |
| `gameobject-create` awareness | Track what you create for cleanup |

### Error Handling

1. **MCP tool missing**: Ask user to configure unity-editor-mcp server
2. **Insufficient data from MCP**: Fall back to explaining what manual steps would be needed
3. **Compilation errors**: Use `console-get-logs` to diagnose
4. **Prefab not saved**: Always call `assets-prefab-save` before `assets-prefab-close`

### Safety Notes

- `gameobject-destroy` permanently removes GameObjects and all children
- `assets-delete` permanently removes assets from the project
- `scene-unload` without saving loses changes
- Never assume instanceID values - always verify with `gameobject-find`

### When MCP Returns Insufficient Data

If `assets-get-data` returns only basic metadata:
- For scenes: Open with `scene-open`, then use `scene-get-data`
- For prefabs: Open with `assets-prefab-open`, then use `gameobject-find`
- Fallback: Inform user that direct YAML reading may be required as last resort

---

## Tool Reference

### assets-find
Search asset database  
**Required:** filter (optional, can be empty)  
**Optional:** searchInFolders, maxResults

### assets-get-data
Get asset serialized data  
**Required:** assetRef (instanceID or assetPath or assetGuid)

### assets-modify
Modify asset file content  
**Required:** assetRef, content (SerializedMember structure)

### assets-prefab-open
Enter prefab edit mode  
**Required:** gameObjectRef

### assets-prefab-save
Save prefab changes  
**Optional:** nothing (uses currently opened prefab)

### assets-prefab-close
Exit prefab edit mode  
**Optional:** save (default false)

### scene-open
Open scene in editor  
**Required:** sceneRef  
**Optional:** loadSceneMode (Single/Additive/AdditiveWithoutLoading)

### scene-save
Save scene to asset  
**Optional:** openedSceneName, path

### scene-unload
Unload scene from editor  
**Required:** name (scene name)

### gameobject-find
Find GameObject in opened prefab or active scene  
**Required:** gameObjectRef (instanceID, path, or name)  
**Optional:** includeComponents, includeHierarchy, hierarchyDepth

### gameobject-create
Create new GameObject  
**Required:** name  
**Optional:** parentGameObjectRef, position, rotation, scale, isLocalSpace

### gameobject-modify
Modify GameObject fields/properties  
**Required:** gameObjectRefs, gameObjectDiffs

### gameobject-component-get
Get component details  
**Required:** gameObjectRef, componentRef  
**Optional:** includeFields, includeProperties, deepSerialization

### gameobject-component-add
Add component to GameObject  
**Required:** gameObjectRef, componentNames (array of full type names)

### gameobject-component-modify
Modify component fields/properties  
**Required:** gameObjectRef, componentRef, componentDiff

### gameobject-component-destroy
Remove component from GameObject  
**Required:** gameObjectRef, destroyComponentRefs

### gameobject-destroy
Destroy GameObject and children  
**Required:** gameObjectRef

### object-modify
Modify any Unity object  
**Required:** objectRef, objectDiff

### editor-application-get-state
Get editor state  
**Optional:** nothing

### editor-application-set-state
Control playmode  
**Optional:** isPlaying, isPaused

### console-get-logs
Get Unity Editor logs  
**Optional:** maxEntries, logTypeFilter, includeStackTrace, lastMinutes

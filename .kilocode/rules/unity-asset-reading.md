# Unity Asset Reading via MCP

When asked to read or inspect a Unity Asset (.asset, .prefab, .unity):

## Core Rules

- **NEVER** directly read the YAML text content of a .asset, .prefab, or .unity file.
- **ALWAYS** use the available unity-editor-mcp tools to get asset information.
- Use `assets-find` to locate the target asset first, then use `assets-get-data` to retrieve detailed information.

## Asset Type Handling

| Asset Type | Recommended Tool | Notes |
|------------|-----------------|-------|
| Material, ScriptableObject, Font, Texture | `assets-get-data` | Returns complete serialized fields and properties |
| Scene (.unity) | `assets-get-data` for metadata | For scene content, use `scene-open` then `scene-get-data` |
| Prefab (.prefab) | `assets-get-data` for basic info | For full hierarchy, use `assets-prefab-open` |

## Scene/Prefab Deep Inspection

If detailed scene or prefab content is required:
1. Use `scene-open` or `assets-prefab-open` to enter edit mode
2. Use `scene-get-data` or `gameobject-find` to inspect contents
3. Use `assets-prefab-close` or `scene-unload` when done

## When MCP Tool Returns Insufficient Data

If `assets-get-data` returns only basic metadata (e.g., for Scene assets), and deeper inspection is needed:
- For scenes: Open the scene first with `scene-open`, then use `scene-get-data`
- For prefabs: Open the prefab first with `assets-prefab-open`, then use `gameobject-find`
- Fallback: If MCP tools are insufficient, inform the user that direct YAML reading may be required as a last resort

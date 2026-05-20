---
name: unity-mcp-assets
description: "Use when searching, creating, modifying, or deleting Unity assets, materials, textures, UI Toolkit elements, or ProBuilder meshes via MCP tools"
---

# Unity MCP Assets Skill

> **前提**: 先熟悉 `unity-mcp-core` — 本 skill 不重复核心规则。

## Quick Reference

| 操作 | 工具 | 关键参数 |
|------|------|---------|
| 搜索资产 | `manage_asset` | `action="search"`, `path`, `search_pattern` |
| 创建资产 | `manage_asset` | `action="create"`, `path`, `asset_type` |
| 修改材质 | `manage_material` | `action="set_material_color"`, `material_path`, `color` |
| 创建纹理 | `manage_texture` | `action="create"`, `path`, `width`, `height` |
| 创建 UI | `manage_ui` | `action="create"`, `path` (UXML/USS) |
| 创建形状 | `manage_probuilder` | `action="create_shape"`, `target` |

## Asset Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_asset` | search | Search asset database by name, type, or filter |
| `manage_asset` | get_info | Get serialized fields and properties of an asset |
| `manage_asset` | modify | Modify asset properties |
| `manage_asset` | create | Create new assets |
| `manage_asset` | delete | Delete assets |
| `manage_asset` | duplicate | Duplicate assets |
| `manage_asset` | move | Move/rename assets |
| `manage_asset` | create_folder | Create folder hierarchy in Assets/ |

## Material Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_material` | create | Create new material |
| `manage_material` | set_material_color | Set material color |
| `manage_material` | set_material_shader_property | Set shader property |
| `manage_material` | assign_material_to_renderer | Assign material to renderer |
| `manage_material` | get_material_info | Get material info |
| `manage_shader` | create | Create shader script |
| `manage_shader` | update | Update shader script |
| `manage_shader` | read | Read shader script |
| `manage_shader` | delete | Delete shader script |

## Texture Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_texture` | create | Create texture with solid fill |
| `manage_texture` | apply_pattern | Apply pattern (checkerboard, stripes, dots, grid, brick) |
| `manage_texture` | apply_gradient | Apply gradient (linear/radial) |
| `manage_texture` | apply_noise | Apply noise (Perlin) |
| `manage_texture` | create_sprite | Create sprite from texture |
| `manage_texture` | set_import_settings | Set import settings |

## UI Toolkit Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_ui` | create | Create UXML/USS files |
| `manage_ui` | attach_ui_document | Attach UIDocument to GameObject |
| `manage_ui` | get_visual_tree | Inspect UI visual tree |
| `manage_ui` | modify_visual_element | Modify UI element properties |
| `manage_ui` | render_ui | Capture UI screenshot |
| `manage_ui` | link_stylesheet | Link USS to UXML |

## ProBuilder Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_probuilder` | create_shape | Create primitive shape |
| `manage_probuilder` | extrude_faces | Extrude faces |
| `manage_probuilder` | bevel_edges | Bevel edges |
| `manage_probuilder` | set_face_material | Assign material to faces |
| `manage_probuilder` | get_mesh_info | Get mesh details |

## Workflow Patterns

### 1. Asset Inspection
```
manage_asset (search) → manage_asset (get_info)
```
1. Use `manage_asset` with `action="search"` to locate asset by name or type.
2. Use `manage_asset` with `action="get_info"` to retrieve serialized properties.

### 2. Asset Modification
```
manage_asset (search) → manage_asset (modify)
```
1. Locate asset with `search`.
2. Modify properties with `modify` using the asset path or GUID.

---
name: unity-mcp-operations
description: "Unity MCP 工具操作规范。涵盖所有 Unity Editor MCP 工具的使用规则、工作流模式与最佳实践。禁止直接读写 Unity YAML 文件，所有资产操作必须通过 MCP 工具完成。"
---

# Unity MCP Agent Operation Rules

This document covers both **inspection** (reading) and **modification** (writing) of Unity assets, scenes, GameObjects, and components via the `unityMCP` MCP server tools.

## Core Rules

### Foundational Principles

1. **NEVER** directly read or edit YAML text content of `.asset`, `.prefab`, `.unity`, `.mat`, `.anim`, `.controller`, `.meta`, or any other Unity-serialized files.
2. **NEVER** use `ReadFile`, `WriteFile`, `StrReplaceFile`, `Glob`, or `Grep` tools on `.asset`, `.prefab`, `.unity`, `.mat`, `.anim`, `.controller`, `.meta` files.
3. **NEVER** manipulate `ProjectSettings/` directory files directly.
4. **ALWAYS** use MCP tools for asset inspection and operations (`manage_asset`, `manage_gameobject`, `manage_components`, etc.).
5. **ALWAYS** pair open/close operations (open prefab stage → close prefab stage, load scene → close/unload scene).
6. **NEVER** assume tools exist — verify MCP server is configured via `debug_request_context` if needed.

### Open/Close Pairing

| Operation | Open | Close / Save |
|-----------|------|--------------|
| Prefab | `manage_prefabs` (open_prefab_stage) | `manage_prefabs` (save_prefab_stage → close_prefab_stage) |
| Scene | `manage_scene` (load) | `manage_scene` (save → close_scene) |

Always save before closing. Leaving prefab edit mode or scene without saving discards changes.

### Tool Priority

- Use **specialized** tools over generic ones (e.g., `manage_asset` over raw file access).
- Use `manage_asset` (search) first to locate assets, then `manage_asset` (get_info) for details.
- Use `find_gameobjects` to locate GameObjects in scenes or opened prefabs.
- Use `batch_execute` for multiple independent operations to reduce latency.

---

## Tool Category Index

### Asset Operations
| Tool | Purpose |
|------|---------|
| `manage_asset` (search) | Search asset database by name, type, or filter |
| `manage_asset` (get_info) | Get serialized fields and properties of an asset |
| `manage_asset` (modify) | Modify asset properties |
| `manage_asset` (create) | Create new assets |
| `manage_asset` (delete) | Delete assets |
| `manage_asset` (duplicate) | Duplicate assets |
| `manage_asset` (move) | Move/rename assets |
| `manage_asset` (create_folder) | Create folder hierarchy in Assets/ |

### GameObject Operations
| Tool | Purpose |
|------|---------|
| `find_gameobjects` | Find GameObjects by name, tag, layer, component type, or path |
| `manage_gameobject` (create) | Create new GameObject |
| `manage_gameobject` (modify) | Modify GameObject properties (name, active, layer, tag, transform) |
| `manage_gameobject` (delete) | Destroy GameObject and children |
| `manage_gameobject` (duplicate) | Duplicate GameObjects |
| `manage_gameobject` (move_relative) | Move GameObject relative to another |
| `manage_gameobject` (look_at) | Orient GameObject to face target |

### Component Operations
| Tool | Purpose |
|------|---------|
| `manage_components` (add) | Add component to GameObject |
| `manage_components` (remove) | Remove component from GameObject |
| `manage_components` (set_property) | Set property values on a component |

### Scene Operations
| Tool | Purpose |
|------|---------|
| `manage_scene` (load) | Load scene (Single or Additive) |
| `manage_scene` (save) | Save scene |
| `manage_scene` (close_scene) | Close/unload scene |
| `manage_scene` (get_hierarchy) | Get scene hierarchy |
| `manage_scene` (get_active) | Get active scene |
| `manage_scene` (get_loaded_scenes) | List loaded scenes |
| `manage_scene` (set_active_scene) | Set active scene |
| `manage_scene` (create) | Create new scene |
| `manage_scene` (validate) | Validate scene integrity |
| `manage_scene` (scene_view_frame) | Frame Scene View on target |

### Prefab Operations
| Tool | Purpose |
|------|---------|
| `manage_prefabs` (open_prefab_stage) | Enter prefab edit mode |
| `manage_prefabs` (save_prefab_stage) | Save prefab changes |
| `manage_prefabs` (close_prefab_stage) | Exit prefab edit mode |
| `manage_prefabs` (create_from_gameobject) | Create prefab from GameObject |
| `manage_prefabs` (get_info) | Get prefab info |
| `manage_prefabs` (get_hierarchy) | Get prefab hierarchy |
| `manage_prefabs` (modify_contents) | Modify prefab contents headlessly |

### Camera Operations
| Tool | Purpose |
|------|---------|
| `manage_camera` (screenshot) | Capture screenshot from camera |
| `manage_camera` (screenshot_multiview) | Multi-angle screenshot capture |
| `manage_camera` (create_camera) | Create camera with preset |
| `manage_camera` (set_target) | Set Follow/LookAt targets |
| `manage_camera` (set_priority) | Set camera priority |
| `manage_camera` (set_lens) | Configure lens (FOV, clip planes, orthographic) |
| `manage_camera` (set_blend) | Configure default blend |
| `manage_camera` (list_cameras) | List all cameras with status |

### Animation Operations
| Tool | Purpose |
|------|---------|
| `manage_animation` (animator_*) | Control Animator (play, crossfade, set parameters) |
| `manage_animation` (controller_*) | Create AnimatorControllers, add states/transitions |
| `manage_animation` (clip_*) | Create clips, add keyframe curves |

### VFX Operations
| Tool | Purpose |
|------|---------|
| `manage_vfx` (particle_*) | ParticleSystem control |
| `manage_vfx` (vfx_*) | VisualEffect control |
| `manage_vfx` (line_*) | LineRenderer control |
| `manage_vfx` (trail_*) | TrailRenderer control |

### Graphics & Rendering
| Tool | Purpose |
|------|---------|
| `manage_graphics` (volume_*) | URP/HDRP volume and post-processing |
| `manage_graphics` (bake_*) | Light baking operations |
| `manage_graphics` (stats_*) | Rendering stats |
| `manage_graphics` (pipeline_*) | Pipeline settings |
| `manage_graphics` (feature_*) | URP renderer features |
| `manage_graphics` (skybox_*) | Skybox and environment settings |

### Physics Operations
| Tool | Purpose |
|------|---------|
| `manage_physics` (raycast) | Raycast query |
| `manage_physics` (raycast_all) | RaycastAll query |
| `manage_physics` (overlap) | Overlap query (sphere, box, capsule) |
| `manage_physics` (add_joint) | Add physics joint |
| `manage_physics` (configure_joint) | Configure joint properties |
| `manage_physics` (get_rigidbody) | Get Rigidbody info |
| `manage_physics` (configure_rigidbody) | Configure Rigidbody properties |
| `manage_physics` (create_physics_material) | Create physics material |
| `manage_physics` (apply_force) | Apply force/torque/explosion |

### Material & Shader
| Tool | Purpose |
|------|---------|
| `manage_material` (create) | Create material |
| `manage_material` (set_material_color) | Set material color |
| `manage_material` (set_material_shader_property) | Set shader property |
| `manage_material` (assign_material_to_renderer) | Assign material |
| `manage_material` (get_material_info) | Get material info |
| `manage_shader` (create) | Create shader script |
| `manage_shader` (update) | Update shader script |
| `manage_shader` (read) | Read shader script |
| `manage_shader` (delete) | Delete shader script |

### Texture Operations
| Tool | Purpose |
|------|---------|
| `manage_texture` (create) | Create texture |
| `manage_texture` (apply_pattern) | Apply pattern (checkerboard, stripes, etc.) |
| `manage_texture` (apply_gradient) | Apply gradient |
| `manage_texture` (apply_noise) | Apply noise |
| `manage_texture` (create_sprite) | Create sprite from texture |
| `manage_texture` (set_import_settings) | Set import settings |

### UI Toolkit Operations
| Tool | Purpose |
|------|---------|
| `manage_ui` (create) | Create UXML/USS |
| `manage_ui` (attach_ui_document) | Attach UIDocument to GameObject |
| `manage_ui` (get_visual_tree) | Inspect UI visual tree |
| `manage_ui` (modify_visual_element) | Modify UI element properties |
| `manage_ui` (render_ui) | Capture UI screenshot |
| `manage_ui` (link_stylesheet) | Link USS to UXML |

### UIToolkit Deep Debug (Custom Tool)

When `manage_ui` (`get_visual_tree`) returns insufficient detail, use the project's custom `uitoolkit_debug` tool via `execute_custom_tool`:

| Action | Purpose | Extra fields beyond standard tool |
|--------|---------|-----------------------------------|
| `list_documents` | List all active `UIDocument` instances in loaded scenes | — |
| `get_tree` | Enhanced VisualElement tree dump | `enabledInHierarchy`, `resolvedStyle`, `controlValue`, `dataBinding` |
| `get_element_detail` | Deep-dive single element | Full `resolvedStyle`, data-binding path, control values (Toggle/Slider/Dropdown etc.) |

**When to use**: Debugging "element not found", "wrong style", "data binding not working", or when `manage_ui get_visual_tree` omits fields you need.

### ProBuilder Operations
| Tool | Purpose |
|------|---------|
| `manage_probuilder` (create_shape) | Create primitive shape |
| `manage_probuilder` (extrude_faces) | Extrude faces |
| `manage_probuilder` (bevel_edges) | Bevel edges |
| `manage_probuilder` (set_face_material) | Assign material to faces |
| `manage_probuilder` (get_mesh_info) | Get mesh details |

### Script Operations
| Tool | Purpose |
|------|---------|
| `create_script` | Create new C# script |
| `delete_script` | Delete C# script |
| `validate_script` | Validate C# script |
| `apply_text_edits` | Apply raw text edits (range-based) |
| `script_apply_edits` | Apply structured edits (method/class) |
| `get_sha` | Get script SHA256 |
| `manage_scriptable_object` (create) | Create ScriptableObject asset |
| `manage_scriptable_object` (modify) | Modify ScriptableObject asset |

### Code Execution
| Tool | Purpose |
|------|---------|
| `execute_code` | Execute arbitrary C# in Unity Editor |
| `batch_execute` | Execute multiple MCP commands in batch |

### Editor Control
| Tool | Purpose |
|------|---------|
| `manage_editor` (play) | Enter Play Mode |
| `manage_editor` (stop) | Exit Play Mode |
| `manage_editor` (pause) | Pause Play Mode |
| `manage_editor` (undo) | Perform Undo |
| `manage_editor` (redo) | Perform Redo |
| `manage_editor` (set_active_tool) | Set active editor tool |

### Build Operations
| Tool | Purpose |
|------|---------|
| `manage_build` (build) | Trigger player build |
| `manage_build` (settings) | Configure build settings |
| `manage_build` (scenes) | Manage build scenes |
| `manage_build` (profiles) | Manage build profiles |

### Package Management
| Tool | Purpose |
|------|---------|
| `manage_packages` (add_package) | Install package |
| `manage_packages` (remove_package) | Remove package |
| `manage_packages` (list_packages) | List installed packages |
| `manage_packages` (search_packages) | Search registry |

### Profiler & Debugging
| Tool | Purpose |
|------|---------|
| `manage_profiler` (profiler_start) | Start profiler |
| `manage_profiler` (get_counters) | Read profiler counters |
| `manage_profiler` (memory_take_snapshot) | Capture memory snapshot |
| `manage_profiler` (frame_debugger_enable) | Enable Frame Debugger |

### Console & Testing
| Tool | Purpose |
|------|---------|
| `read_console` (get) | Retrieve Unity Editor console logs |
| `read_console` (clear) | Clear console logs |
| `run_tests` | Run Unity tests |
| `get_test_job` | Poll test run status |

### Documentation & Reflection
| Tool | Purpose |
|------|---------|
| `unity_docs` (get_doc) | Fetch Unity ScriptReference docs |
| `unity_docs` (lookup) | Search Unity docs |
| `unity_reflect` (get_type) | Reflect C# type members |
| `unity_reflect` (search) | Search types across assemblies |

### Utilities
| Tool | Purpose |
|------|---------|
| `refresh_unity` | Refresh AssetDatabase and optionally compile |
| `find_in_file` | Search file with regex |
| `set_active_instance` | Set active Unity instance |
| `manage_tools` (list_groups) | List available tool groups |
| `execute_menu_item` | Execute Unity menu item |
| `execute_custom_tool` | Execute custom registered tool |
| `debug_request_context` | Get MCP request context |

---

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

### 3. Prefab Inspection
```
manage_prefabs (open_prefab_stage) → find_gameobjects → manage_prefabs (close_prefab_stage)
```
1. Open prefab with `open_prefab_stage`.
2. Find target GameObject with `find_gameobjects`.
3. Exit prefab mode with `close_prefab_stage` (no save needed for inspection).

### 4. Prefab Editing
```
manage_prefabs (open_prefab_stage) → find_gameobjects → manage_components (set_property) → manage_prefabs (save_prefab_stage) → manage_prefabs (close_prefab_stage)
```
1. Open prefab with `open_prefab_stage`.
2. Locate target with `find_gameobjects`.
3. Modify component with `manage_components` (add/remove/set_property).
4. Save changes with `save_prefab_stage`.
5. Exit with `close_prefab_stage`.

### 5. Scene Inspection
```
manage_scene (load) → find_gameobjects / manage_scene (get_hierarchy) → manage_scene (close_scene)
```
1. Open scene with `manage_scene` (load).
2. Inspect with `get_hierarchy` or `find_gameobjects`.
3. Unload when done with `close_scene`.

### 6. Scene Editing
```
manage_scene (load) → find_gameobjects → manage_gameobject / manage_components → manage_scene (save) → manage_scene (close_scene)
```
1. Load scene.
2. Find GameObjects.
3. Modify GameObjects or components.
4. Save scene.
5. Close scene.

### 7. GameObject Creation
```
manage_gameobject (create) → manage_components (add) → manage_prefabs (create_from_gameobject)
```
1. Create empty GameObject with `manage_gameobject` (create).
2. Add components with `manage_components` (add).
3. Optionally save as prefab with `manage_prefabs` (create_from_gameobject).

### 8. Component Management
```
find_gameobjects → manage_components (add / remove / set_property)
```
1. Find GameObject with `find_gameobjects`.
2. Add, remove, or modify component properties as needed.

### 9. Script Creation
```
create_script
```
1. Create script with `create_script`.
2. **Must call `refresh_unity` with compile="request" after script creation.**

### 10. Script Editing
```
apply_text_edits / script_apply_edits
```
1. Apply edits with `apply_text_edits` (range-based) or `script_apply_edits` (structured method/class edits).
2. **Must call `refresh_unity` with compile="request" after script edits.**

### 11. Playmode Testing
```
manage_editor (play) → verify → manage_editor (stop)
```
1. Check current state if needed.
2. Start playmode with `manage_editor` (play).
3. Test functionality.
4. Stop playmode with `manage_editor` (stop).

### 12. Batch Operations
```
batch_execute
```
1. When creating/modifying multiple independent objects, use `batch_execute` to reduce round-trip latency.
2. Example: creating 5 cubes → one `batch_execute` with 5 `manage_gameobject` (create) commands.

### 13. Material Creation
```
manage_material (create) → manage_material (set_material_shader_property / set_material_color) → manage_material (assign_material_to_renderer)
```
1. Create material with `manage_material` (create).
2. Set shader properties and color.
3. Assign to renderer.

### 14. Texture Generation
```
manage_texture (create) → manage_texture (apply_pattern / apply_gradient / apply_noise) → manage_texture (set_import_settings)
```
1. Create texture.
2. Apply procedural content.
3. Configure import settings.

### 15. UI Toolkit Workflow
```
manage_ui (create UXML) → manage_ui (create USS) → manage_ui (link_stylesheet) → manage_ui (attach_ui_document)
```
1. Create UXML structure.
2. Create USS stylesheet.
3. Link stylesheet to UXML.
4. Attach UIDocument to a GameObject.

### 16. Folder Reorganization
```
manage_asset (create_folder) → manage_asset (move)
```
1. **DO NOT** use filesystem commands (`mkdir`, `move`, etc.) to manipulate folders.
2. Use `manage_asset` (create_folder) to create destination.
3. Use `manage_asset` (move) to move assets.
4. Verify with `manage_asset` (search) after moves complete.

---

## Best Practices

### Pairing Rules

| Always Pair | Reason |
|-------------|--------|
| `manage_prefabs` (open_prefab_stage) → `manage_prefabs` (close_prefab_stage) | Leave prefab edit mode; unsaved changes are discarded |
| `manage_scene` (load) → `manage_scene` (close_scene) | Free editor memory |
| `manage_editor` (play) → `manage_editor` (stop) | Clean exit from playmode |

### Error Handling

1. **MCP tool missing or failing**: Use `debug_request_context` to check connection. Ask user to verify Unity Editor is running with MCP server enabled.
2. **Compilation errors after script edit**: Use `read_console` (get) to diagnose errors.
3. **Prefab not saved**: Always call `save_prefab_stage` before `close_prefab_stage`.
4. **Scene changes lost**: Always call `save` before `close_scene`.
5. **AssetDatabase stale**: Call `refresh_unity` before operations if files were recently created.

### Safety Notes

- `manage_gameobject` (delete) permanently removes GameObjects and all children.
- `manage_asset` (delete) permanently removes assets from the project (including empty folders).
- `manage_scene` (close_scene) without saving loses changes.
- Never assume instanceID values — always verify with `find_gameobjects`.
- **DO NOT use filesystem commands** (`mkdir`, `rmdir`, `rm`, `move`, etc.) to manipulate Unity assets or folders — always use `manage_asset` MCP tools.

### Performance Tips

- Use `batch_execute` for multiple independent operations — reduces latency by 10–100x compared to sequential calls.
- Use `find_gameobjects` with specific filters (name, tag, component) rather than fetching entire scene hierarchies.
- Use `manage_asset` (search) with `page_size` to paginate large result sets.
- Prefer `script_apply_edits` over `apply_text_edits` for method/class-level changes — safer boundaries and structural validation.

### When MCP Returns Insufficient Data

If `manage_asset` (get_info) returns only basic metadata:
- For scenes: Load with `manage_scene` (load), then use `manage_scene` (get_hierarchy) or `find_gameobjects`.
- For prefabs: Open with `manage_prefabs` (open_prefab_stage), then use `find_gameobjects`.
- For components: Use `manage_components` (set_property) with the component index discovered via `find_gameobjects` components resource.

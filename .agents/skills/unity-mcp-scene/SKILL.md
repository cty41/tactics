---
name: unity-mcp-scene
description: "Use when loading, saving, inspecting, or editing Unity scenes, prefabs, and cameras via MCP tools"
---

# Unity MCP Scene, Prefab & Camera Skill

> **Prerequisite**: `unity-mcp-core` — 所有 MCP 工具调用必须遵循其中的通用规则（路径格式、搜索方法、Open/Close 配对）。

## Quick Reference

| 操作 | 工具 | 关键参数 |
|------|------|---------|
| 加载场景 | `manage_scene` | `action="load"`, `path` (或 `build_index`) |
| 保存场景 | `manage_scene` | `action="save"` |
| 关闭场景 | `manage_scene` | `action="close_scene"` |
| 查看层级 | `manage_scene` | `action="get_hierarchy"` |
| 创建场景 | `manage_scene` | `action="create"`, `name` |
| 打开 Prefab | `manage_prefabs` | `action="open_prefab_stage"`, `prefab_path` |
| 保存 Prefab | `manage_prefabs` | `action="save_prefab_stage"` |
| 关闭 Prefab | `manage_prefabs` | `action="close_prefab_stage"` |
| 头尾修改 Prefab | `manage_prefabs` | `action="modify_contents"`, `prefab_path` |
| 截图 | `manage_camera` | `action="screenshot"` (可选 `include_image=true`) |

> 核心规则见 `unity-mcp-core`

## When to use

- 需要加载、保存、关闭、检查或验证 Unity 场景时
- 需要打开、保存、关闭或无头修改 Prefab 时
- 需要创建、配置或截图相机时
- 需要保证 Scene/Prefab 操作符合 Open/Close 配对规则时

## Scene Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_scene` | load | Load scene (Single 或 Additive) |
| `manage_scene` | save | Save scene |
| `manage_scene` | close_scene | Close/unload scene |
| `manage_scene` | get_hierarchy | Get scene hierarchy (支持分页/深度控制) |
| `manage_scene` | get_active | Get active scene |
| `manage_scene` | get_loaded_scenes | List all loaded scenes |
| `manage_scene` | set_active_scene | Set active scene |
| `manage_scene` | create | Create new scene (可选 template) |
| `manage_scene` | validate | Validate scene integrity (可选 auto_repair) |
| `manage_scene` | scene_view_frame | Frame Scene View on target |

### `manage_scene` — 关键参数说明

| 参数 | 类型 | 说明 |
|------|------|------|
| `action` | string | 见上表 |
| `path` | string | 场景 asset 路径，如 `Assets/Scenes/Game.unity` |
| `build_index` | int | Build Settings 中的场景索引（替代 path） |
| `name` | string | 场景名称（create/close_scene/set_active_scene） |
| `additive` | bool | Additive 模式加载（默认 false） |
| `template` | string | 模板场景路径（create 时） |
| `scene_name` | string | 用于 set_active_scene 的场景名 |
| `auto_repair` | bool | validate 时自动修复问题 |
| `page_size` | int | get_hierarchy 分页大小 |
| `max_depth` | int | get_hierarchy 最大深度 |
| `max_children_per_node` | int | get_hierarchy 每节点最大子项 |
| `max_nodes` | int | get_hierarchy 总节点数上限 |
| `include_transform` | bool | get_hierarchy 是否包含变换信息 |
| `remove_scene` | bool | close_scene 时是否从磁盘移除 |
| `scene_view_target` | string/int | scene_view_frame 的目标 GameObject |

## Prefab Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_prefabs` | open_prefab_stage | Enter prefab edit mode |
| `manage_prefabs` | save_prefab_stage | Save prefab changes |
| `manage_prefabs` | close_prefab_stage | Exit prefab edit mode |
| `manage_prefabs` | create_from_gameobject | Create prefab from GameObject |
| `manage_prefabs` | get_info | Get prefab metadata |
| `manage_prefabs` | get_hierarchy | Get prefab hierarchy |
| `manage_prefabs` | modify_contents | Modify prefab contents headlessly |

### `manage_prefabs` — 关键参数说明

| 参数 | 类型 | 说明 |
|------|------|------|
| `prefab_path` | string | Prefab asset 路径，如 `Assets/Prefabs/Enemy.prefab` |
| `create_child` | object/array | 添加子对象（单个或数组，支持 nested prefab） |
| `delete_child` | string/array | 删除子对象（名称或路径数组） |
| `component_properties` | dict | 修改现有组件属性，如 `{"Rigidbody": {"mass": 5.0}}` |
| `components_to_add` | string[] | 要添加的组件类型列表 |
| `components_to_remove` | string[] | 要移除的组件类型列表 |
| `position` | float[3] | 位置 `[x, y, z]` |
| `rotation` | float[3] | 欧拉角 `[x, y, z]` |
| `scale` | float[3] | 缩放 `[x, y, z]` |
| `name` | string | 名称 |
| `tag` | string | 标签 |
| `layer` | string | 图层 |
| `set_active` | bool | 激活/禁用 |
| `allow_overwrite` | bool | create_from_gameobject 允许覆盖 |
| `unlink_if_instance` | bool | create_from_gameobject 时断开实例连接 |
| `search_inactive` | bool | get_hierarchy 包含非激活对象 |

## Camera Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_camera` | screenshot | Capture screenshot |
| `manage_camera` | screenshot_multiview | Multi-angle screenshot capture |
| `manage_camera` | create_camera | Create camera with preset |
| `manage_camera` | set_target | Set Follow/LookAt targets |
| `manage_camera` | set_priority | Set camera priority |
| `manage_camera` | set_lens | Configure lens (FOV, clip planes, orthographic) |
| `manage_camera` | set_body | Configure Body component |
| `manage_camera` | set_aim | Configure Aim component |
| `manage_camera` | set_noise | Configure Noise component |
| `manage_camera` | add_extension | Add extension (Confiner, Deoccluder, ImpulseListener, etc.) |
| `manage_camera` | remove_extension | Remove extension |
| `manage_camera` | set_blend | Configure default blend |
| `manage_camera` | force_camera | Override Brain to use specific camera |
| `manage_camera` | release_override | Release camera override |
| `manage_camera` | list_cameras | List all cameras with status |
| `manage_camera` | ping | Check Cinemachine availability |
| `manage_camera` | ensure_brain | Ensure CinemachineBrain exists |
| `manage_camera` | get_brain_status | Get Brain state (active camera, blend) |

### `manage_camera` — 截图参数说明

| 参数 | 类型 | 说明 |
|------|------|------|
| `camera` | string/int | 指定相机（省略则用 ScreenCapture API 捕获全部层含 UI） |
| `include_image` | bool | 返回内嵌 base64 PNG |
| `max_resolution` | int | 内嵌图片最大边长（默认 640） |
| `screenshot_file_name` | string | 文件名（可选，默认时间戳） |
| `screenshot_super_size` | int | 超采样倍率 |
| `capture_source` | string | `game_view`（默认）或 `scene_view` |
| `batch` | string | `surround`（6 角度）或 `orbit`（可配置网格） |
| `view_target` | string/int/float[3] | 聚焦目标（GameObject 或世界坐标） |
| `view_position` | float[3] | 相机位置 `[x, y, z]` |
| `view_rotation` | float[3] | 欧拉旋转 `[x, y, z]` |
| `orbit_angles` | int | 方位角采样数（batch=orbit，默认 8，最大 36） |
| `orbit_elevations` | float[] | 仰角数组（batch=orbit，默认 [0, 30, -15]） |
| `orbit_distance` | float | 相机距离（batch=orbit，默认 auto） |
| `orbit_fov` | float | 相机 FOV（batch=orbit，默认 60） |

### `manage_camera` — 创建/配置参数说明

| 参数 | 类型 | 说明 |
|------|------|------|
| `target` | string/int | 目标相机或 GameObject |
| `search_method` | string | `by_id` / `by_name` / `by_path` |
| `properties` | dict | 动作特定参数（见各 action 的 properties 说明） |

## Workflow

### 1. Prefab Inspection（只读检查）
```
manage_prefabs (open_prefab_stage) → find_gameobjects → manage_prefabs (close_prefab_stage)
```
1. 打开 prefab: `manage_prefabs action="open_prefab_stage" prefab_path="Assets/..."`.
2. 查找目标: `find_gameobjects search_term="..."`.
3. 退出 prefab 模式: `manage_prefabs action="close_prefab_stage"`（无需保存）.

### 2. Prefab Editing（头尾修改）
```
manage_prefabs (open_prefab_stage) → find_gameobjects → manage_components (set_property) → manage_prefabs (save_prefab_stage) → manage_prefabs (close_prefab_stage)
```
1. 打开 prefab: `manage_prefabs action="open_prefab_stage" prefab_path="Assets/..."`.
2. 定位目标: `find_gameobjects search_term="..."`.
3. 修改组件: `manage_components action="set_property" target=... component_type="..." property="..." value=...`.
4. 保存: `manage_prefabs action="save_prefab_stage"`.
5. 退出: `manage_prefabs action="close_prefab_stage"`.

### 3. Prefab Editing（无头修改）
```
manage_prefabs (modify_contents) — 无需打开 Stage，直接修改
```
1. 添加子对象: `manage_prefabs action="modify_contents" prefab_path="Assets/..." create_child=[{"name": "Child1", "primitive_type": "Sphere"}]`.
2. 删除子对象: `manage_prefabs action="modify_contents" prefab_path="Assets/..." delete_child=["Child1"]`.
3. 修改组件: `manage_prefabs action="modify_contents" prefab_path="Assets/..." component_properties={"Rigidbody": {"mass": 5.0}}`.

> **建议**: 简单修改用 `modify_contents`，复杂编辑用 `open_prefab_stage` + `manage_components`。

### 4. Scene Inspection
```
manage_scene (load) → find_gameobjects / manage_scene (get_hierarchy) → manage_scene (close_scene)
```
1. 加载场景: `manage_scene action="load" path="Assets/Scenes/..."`（可选 `additive=true`）.
2. 检查层级: `manage_scene action="get_hierarchy" max_depth=3`.
3. 查找对象: `find_gameobjects search_term="..."`.
4. 卸载场景: `manage_scene action="close_scene"`.

### 5. Scene Editing
```
manage_scene (load) → find_gameobjects → manage_gameobject / manage_components → manage_scene (save) → manage_scene (close_scene)
```
1. 加载场景.
2. 查找 GameObjects.
3. 修改 GameObject 或组件.
4. 保存场景.
5. 关闭场景.

### 6. Screenshot Capture
```
manage_camera (screenshot) — 快速截图
manage_camera (screenshot) batch="surround" — 6 角度截图
manage_camera (screenshot) batch="orbit" — 可配置网格截图
```
- 默认（不指定 `camera`）使用 ScreenCapture API，捕获所有渲染层包括 Screen Space - Overlay UI.
- 指定 `camera` 使用相机渲染，**不包含** Screen Space - Overlay Canvas.
- 使用 `capture_source="scene_view"` 捕获 Scene View 视口.

### 7. Camera Creation & Setup
```
manage_camera (create_camera) → manage_camera (set_target) → manage_camera (set_lens) → manage_camera (set_blend)
```
1. 创建相机: `manage_camera action="create_camera" properties={"preset": "third_person"}`.
2. 设置目标: `manage_camera action="set_target" target="CameraName" properties={"follow": "Player", "lookAt": "Player"}`.
3. 配置镜头: `manage_camera action="set_lens" target="CameraName" properties={"fieldOfView": 60}`.
4. 配置混合: `manage_camera action="set_blend" properties={"style": "EaseInOut", "duration": 1.0}`.

## Anti-patterns

| Wrong | Correct | Why |
|-------|---------|-----|
| Loading a scene and leaving it open | Save if changed, then close | Avoids hidden editor state |
| Opening Prefab Stage without closing it | Save/close or close without save after read-only work | Keeps editor state clean |
| Capturing camera render when overlay UI is required | Omit `camera` to use ScreenCapture | Camera render excludes Screen Space Overlay |
| Editing prefab YAML directly | Use `manage_prefabs` | Preserves Unity serialization |

## Checklist

- [ ] Scene loads have matching save/close steps when mutated
- [ ] Prefab Stage opens have matching save/close or close-only steps
- [ ] Screenshots choose ScreenCapture vs camera render intentionally
- [ ] Read-only inspection avoids unnecessary saves
- [ ] Mutating scene/prefab work is verified after changes

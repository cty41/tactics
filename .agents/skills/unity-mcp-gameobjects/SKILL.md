---
name: unity-mcp-gameobjects
description: "Use when creating, modifying, deleting, or finding GameObjects and components in Unity via MCP tools"
---

# Unity MCP GameObjects & Components Skill

> **Prerequisite**: `unity-mcp-core` — 所有 MCP 工具调用必须遵循其中的通用规则（路径格式、搜索方法、权限检查）。

## Quick Reference

| 操作 | 工具 | 关键参数 |
|------|------|---------|
| 创建物体 | `manage_gameobject` | `action="create"`, `name`, `primitive_type` |
| 修改属性 | `manage_gameobject` | `action="modify"`, `target`, `name`/`tag`/`layer`/`position` |
| 删除物体 | `manage_gameobject` | `action="delete"`, `target` |
| 复制物体 | `manage_gameobject` | `action="duplicate"`, `target` |
| 搜索物体 | `find_gameobjects` | `search_term`, `search_method` (by_name/by_tag/by_component) |
| 添加组件 | `manage_components` | `action="add"`, `target`, `component_type` |
| 修改属性 | `manage_components` | `action="set_property"`, `target`, `component_type`, `property`, `value` |
| 移除组件 | `manage_components` | `action="remove"`, `target`, `component_type` |

> 核心规则见 `unity-mcp-core`

## When to use

- 需要在 Unity 场景或 Prefab Stage 中查找、创建、修改、删除 GameObject 时
- 需要添加、移除或设置组件属性时
- 需要批量创建或调整多个对象时
- 需要通过 instance ID 精确操作对象以避免名称歧义时

## GameObject Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `find_gameobjects` | search | Find GameObjects by name, tag, layer, component type, or path |
| `manage_gameobject` | create | Create new GameObject (optionally with primitive_type) |
| `manage_gameobject` | modify | Modify GameObject properties (name, active, layer, tag, transform) |
| `manage_gameobject` | delete | Destroy GameObject and children |
| `manage_gameobject` | duplicate | Duplicate GameObjects |
| `manage_gameobject` | move_relative | Move GameObject relative to another |
| `manage_gameobject` | look_at | Orient GameObject to face target |

### `manage_gameobject` — 关键参数说明

| 参数 | 类型 | 说明 |
|------|------|------|
| `action` | string | `create` / `modify` / `delete` / `duplicate` / `move_relative` / `look_at` |
| `target` | string/int | 目标 GameObject 的 instance ID（优先）或 name/path |
| `search_method` | string | `by_id` / `by_name` / `by_path` / `by_tag` / `by_layer` / `by_component` |
| `name` | string | 创建或重命名时的名称 |
| `primitive_type` | string | 原始体类型: `Cube` / `Sphere` / `Capsule` / `Cylinder` / `Plane` / `Quad` |
| `position` | float[3] | 世界坐标 `[x, y, z]` |
| `rotation` | float[3] | 欧拉角 `[x, y, z]` |
| `scale` | float[3] | 缩放 `[x, y, z]` |
| `parent` | string/int | 父级 GameObject 的 instance ID 或 name |
| `tag` | string | 标签名称 |
| `layer` | string | 图层名称 |
| `set_active` | bool | 激活/禁用 |
| `is_static` | bool | 静态标记 |
| `new_name` | string | 重命名目标（modify 时） |
| `save_as_prefab` | bool | 是否同时保存为 Prefab |
| `prefab_path` | string | Prefab 保存路径 |

### `find_gameobjects` — 搜索方式

| 搜索方式 | 说明 | 示例 |
|----------|------|------|
| `by_name` | 按名称搜索（支持模糊） | `search_term="Enemy"` |
| `by_tag` | 按标签搜索 | `search_term="Player"` |
| `by_layer` | 按图层搜索 | `search_term="Water"` |
| `by_component` | 按组件类型搜索 | `search_term="Rigidbody"` |
| `by_path` | 按场景层级路径搜索 | `search_term="Env/Buildings/House"` |
| `by_id` | 按 instance ID 精确搜索 | `search_term="12345"` |

支持 `include_inactive` 参数搜索非激活物体，`page_size` / `cursor` 分页。

## Component Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_components` | add | Add component to GameObject |
| `manage_components` | remove | Remove component from GameObject |
| `manage_components` | set_property | Set property values on a component |

### `manage_components` — 关键参数说明

| 参数 | 类型 | 说明 |
|------|------|------|
| `action` | string | `add` / `remove` / `set_property` |
| `target` | string/int | 目标 GameObject 的 instance ID（优先）或 name/path |
| `component_type` | string | 组件类型名，如 `Rigidbody` / `BoxCollider` / `MyScript` |
| `component_index` | int | 当同一类型有多个组件时的索引（0-based），省略则取第一个 |
| `property` | string | 要设置的属性名（仅 set_property） |
| `value` | varied | 属性值（仅 set_property） |
| `properties` | dict | 批量设置属性键值对 |

### 对象引用赋值

`set_property` 中 `value` 支持的对象引用格式：

| 目标类型 | value 格式 |
|----------|-----------|
| GameObject / Component | `{"instanceID": 12345}` |
| Asset 资源 | `{"guid": "..."}` 或 `{"path": "Assets/..."}` |
| Sprite 子资源 | `{"guid": "...", "spriteName": "name"}` |

## Workflow

### 1. GameObject Creation

```
manage_gameobject (create) → manage_components (add) → manage_prefabs (create_from_gameobject)
```

1. **创建空物体或原始体**：使用 `manage_gameobject` 的 `create`，可指定 `primitive_type` 创建基本几何体。
2. **添加组件**：使用 `manage_components` 的 `add` 添加所需组件。
3. **设置属性**：使用 `manage_components` 的 `set_property` 或 `properties` 批量设置。
4. **保存为 Prefab**（可选）：使用 `manage_prefabs` 的 `create_from_gameobject` 保存。

### 2. Component Management

```
find_gameobjects → manage_components (add / remove / set_property)
```

1. **定位目标**：使用 `find_gameobjects` 按名称/标签/组件类型搜索。
2. **批量修改**：对同一目标做多个组件操作时，使用 `batch_execute` 合并调用。
3. **读取组件数据**：通过 `mcpforunity://scene/gameobject/{id}/components` 资源 URI 读取组件数据（只读）。
4. **单组件查询**：通过 `mcpforunity://scene/gameobject/{id}/component/{name}` 获取单个组件的完整属性。

### 3. Efficient Batch Operations

当需要对多个 GameObject 执行相同操作时，使用 `batch_execute`：

```
batch_execute (commands=[{tool, params}, ...])
```

示例：一次创建多个物体并分别设置位置：
```json
{
  "commands": [
    {"tool": "manage_gameobject", "params": {"action": "create", "name": "Cube1", "primitive_type": "Cube", "position": [0, 0, 0]}},
    {"tool": "manage_gameobject", "params": {"action": "create", "name": "Cube2", "primitive_type": "Cube", "position": [2, 0, 0]}},
    {"tool": "manage_gameobject", "params": {"action": "create", "name": "Cube3", "primitive_type": "Cube", "position": [4, 0, 0]}}
  ]
}
```

### 4. Prefab 实例化

从已有 Prefab 创建实例属于 `manage_prefabs` 范畴（详参 `unity-mcp-scene`），但最终布局调整仍使用 `manage_gameobject`：

```
find_gameobjects (找到实例) → manage_gameobject (modify 调整 transform/active/layer)
```

## Anti-patterns

| Wrong | Correct | Why |
|-------|---------|-----|
| Targeting by name when IDs are available | Use instance ID | Avoids ambiguous names |
| Editing prefab instances without prefab workflow | Use `unity-mcp-scene` prefab operations | Preserves prefab asset changes |
| Running repeated object edits sequentially | Use `batch_execute` | Reduces latency |
| Setting component references by guessed strings | Use instance ID or asset reference object | Avoids broken references |

## Checklist

- [ ] Target GameObjects were found before mutation
- [ ] Instance IDs are used when available
- [ ] Inactive objects are included when needed
- [ ] Multiple same-type components specify `component_index`
- [ ] Repeated independent operations use `batch_execute`

## 注意事项

- **instance ID 优先**：所有 `target` 参数优先使用 instance ID（来自 `find_gameobjects` 返回），避免名称歧义。
- **搜索范围**：`find_gameobjects` 默认只搜索当前加载场景中的活跃物体；设置 `include_inactive=true` 可搜索非激活物体。
- **组件索引**：同一 GameObject 上可能存在多个相同类型的组件（如多个 Collider），使用 `component_index` 指定目标。
- **批量优先**：连续多个无关操作优先使用 `batch_execute` 合并为一次调用，提升性能。
- **单次只做一件事**：一个 MCP 工具调用只做逻辑上的一步操作；不要试图在一次调用中完成创建+加组件+设属性等复合操作。

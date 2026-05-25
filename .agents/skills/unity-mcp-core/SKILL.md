---
name: unity-mcp-core
description: "Use when operating Unity Editor via MCP tools — core rules, foundational principles, and common patterns that apply to ALL operations"
---

# unity-mcp-core

## Quick Reference

| 规则 | 说明 |
|------|------|
| 禁止直接读写 YAML | 不碰 .asset/.prefab/.unity/.mat/.meta 文件 |
| 必须使用 MCP 工具 | `manage_asset`, `manage_gameobject`, `manage_components` 等 |
| Open/Close 配对 | 打开 Prefab/Scene → 保存 → 关闭 |
| 批量操作用 batch | `batch_execute` 减少延迟 |
| 先搜索再操作 | `find_gameobjects` → `manage_*` |

**子技能索引**：
| 领域 | 加载技能 |
|------|---------|
| GameObject + Component | `unity-mcp-gameobjects` |
| Scene + Prefab + Camera | `unity-mcp-scene` |
| Asset + Material + Texture | `unity-mcp-assets` |
| Script + Build + Profiler + Debug | `unity-mcp-advanced` |

---

## When to use

- 需要通过 MCP 操作 Unity Editor、场景、Prefab、材质、脚本或构建时
- 不确定应该使用哪个 Unity MCP 子技能时
- 需要确认 Unity YAML、Prefab Stage、Scene 加载和批量操作的安全规则时
- 需要在 Codex 或 OpenCode 中保持一致的 Unity Editor 操作流程时

## Workflow

1. 先判断目标是否是 Unity 序列化资产或 Editor 状态。
2. 加载本技能确认核心规则，再按领域加载子技能。
3. 用搜索/读取工具定位目标对象，不直接读写 Unity YAML。
4. 对 3 个以上独立操作使用 `batch_execute`。
5. 对打开的 Prefab 或 Scene 执行保存/关闭配对。

## Core Rules

### Foundational Principles

1. **NEVER** directly read or edit YAML text content of `.asset`, `.prefab`, `.unity`, `.mat`, `.anim`, `.controller`, `.meta`, or any other Unity-serialized files。
2. **NEVER** use `ReadFile`, `WriteFile`, `StrReplaceFile`, `Glob`, or `Grep` tools on `.asset`, `.prefab`, `.unity`, `.mat`, `.anim`, `.controller`, `.meta` files。
3. **NEVER** manipulate `ProjectSettings/` directory files directly。
4. **ALWAYS** use MCP tools for asset inspection and operations (`manage_asset`, `manage_gameobject`, `manage_components`, etc.)。
5. **ALWAYS** pair open/close operations (open prefab stage → close prefab stage, load scene → close/unload scene)。
6. **NEVER** assume tools exist — verify MCP server is configured via `debug_request_context` if needed。

### Open/Close Pairing

| Operation | Open | Close / Save |
|-----------|------|--------------|
| Prefab | `manage_prefabs` (open_prefab_stage) | `manage_prefabs` (save_prefab_stage → close_prefab_stage) |
| Scene | `manage_scene` (load) | `manage_scene` (save → close_scene) |

Always save before closing。 Leaving prefab edit mode or scene without saving discards changes。

### Tool Priority

- Use **specialized** tools over generic ones (e.g., `manage_asset` over raw file access)。
- Use `manage_asset` (search) first to locate assets, then `manage_asset` (get_info) for details。
- Use `find_gameobjects` to locate GameObjects in scenes or opened prefabs。
- Use `batch_execute` for multiple independent operations to reduce latency。

### Batch Execution

```json
{
  "commands": [
    {"tool": "manage_gameobject", "params": {"action": "create", "name": "Obj1", "primitive_type": "Cube"}},
    {"tool": "manage_gameobject", "params": {"action": "create", "name": "Obj2", "primitive_type": "Sphere"}}
  ]
}
```

Use `batch_execute` when performing 3+ independent operations。 Reduces latency and token costs by 10-100x compared to sequential calls。

## Anti-patterns

| Wrong | Correct | Why |
|-------|---------|-----|
| Editing `.prefab` text directly | Use `manage_prefabs` or asset MCP tools | Preserves Unity serialization |
| Loading a scene and leaving it open | Save and close/unload it | Avoids hidden editor state |
| Repeating many single MCP calls | Use `batch_execute` | Reduces latency and token cost |
| Assuming a tool exists | Verify available MCP tools or context | Prevents dead-end tool calls |

## Checklist

- [ ] No Unity YAML file was read or edited directly
- [ ] Correct domain child skill was loaded when needed
- [ ] Targets were searched/read before mutation
- [ ] Open prefab/scene operations have save and close steps
- [ ] Batch operations use `batch_execute` when appropriate

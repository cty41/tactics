---
name: unity-auto-compile-guard
description: "Use when editing, creating, renaming, moving, or deleting Unity C# scripts and you must enforce post-edit compilation consistently across Codex and OpenCode"
---

# unity-auto-compile-guard

## Quick Reference

| 场景 | 约束 |
|------|------|
| 修改 `.cs` | 必须调用 `refresh_unity(compile=\"request\")` |
| 多次修改 `.cs` | 以最后一次修改后的编译为准 |
| 只改非 `.cs` | 本规则不触发 |

## When to use

- 修改、创建、删除、重命名或移动 Unity C# 脚本时
- 需要在 Codex 与 OpenCode 中复用同一套“修改后必须编译”的规则时
- 需要判断当前任务是否可以在未编译的情况下结束时

## Workflow

### Step 1: 读取共享规则源

先读取：

- `../../shared-rules/unity-auto-compile.md`

这个文件是本约束的唯一规则正文来源。不要在其他 skill、插件或 hook 里复制并长期维护另一份业务规则文本。

### Step 2: 判断是否触发

仅在以下情况触发：

- 新增 `.cs`
- 编辑 `.cs`
- 删除 `.cs`
- 重命名、移动 `.cs`

以下情况默认不触发：

- `.md`
- `.uxml`
- `.uss`
- 美术资源
- 普通配置和非 C# 资产

### Step 3: 执行动作

一旦触发：

- 在完成该轮脚本改动后调用 `refresh_unity(compile="request")`
- 如果之后再次修改任何 `.cs`，必须再次编译

### Step 4: 结束前复核

结束任务前确认：

- 最近一次 `.cs` 修改之后，已经调用过 `refresh_unity(compile="request")`
- 如果没有，不得把任务标记为完成

## Anti-patterns

| ❌ 错误 | ✅ 正确 | 原因 |
|---------|---------|------|
| 先改 `.cs`，直接结束 | 改完后调用 `refresh_unity(compile=\"request\")` | 否则 Unity 编译状态未确认 |
| 只编译一次，后面继续改 `.cs` | 最后一次 `.cs` 修改后再次编译 | 编译必须覆盖最新改动 |
| 在插件和 skill 中各维护一份规则正文 | 统一引用共享规则文件 | 避免双端规则漂移 |

## Checklist

- [ ] 已读取共享规则文件
- [ ] 仅在 `.cs` 改动时触发
- [ ] 最近一次 `.cs` 修改后已调用 `refresh_unity(compile="request")`
- [ ] 没有在别处复制维护另一份业务规则正文

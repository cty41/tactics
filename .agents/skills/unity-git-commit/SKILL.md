---
name: unity-git-commit
description: Use when committing Unity project changes, creating PRs, or handling git operations in a Unity codebase where .meta files must be paired with their source files
---

# Unity Git 提交规范

## Quick Reference

| 操作 | 需同时处理的 .meta |
|------|------------------|
| 新增文件 | 确认 .meta 已生成且 staged |
| 修改文件 | .meta 通常不变，检查是否误改 |
| 删除文件 | 同时删除对应 .meta |
| 移动/重命名 | 同时移动/重命名 .meta（保持 GUID 不变） |

**提交前检查**：
1. `git status` 确认 .meta 与源文件成对出现
2. 检查 GUID 是否冲突（Unity 导入时自动处理）
3. 检查 ProjectSettings/ 下文件是否有意外改动

## .meta 文件关联规则 (CRITICAL)

**Unity 的每个 Assets 下的文件都有一个同名的 `.meta` 文件。添加/修改/删除源文件时，必须同时处理对应的 `.meta` 文件。**

| 操作 | 源文件 | .meta 文件 |
|------|--------|-----------|
| 新增文件 | `git add Foo.cs` | **必须** `git add Foo.cs.meta` |
| 修改文件 | `git add Foo.cs` | .meta 未变则无需 add |
| 删除文件 | `git rm Foo.cs` | **必须** `git rm Foo.cs.meta` |
| 重命名 | `git mv Old.cs New.cs` | `git mv Old.cs.meta New.cs.meta` |

## 提交前检查

每次 git add 后必须确认：

1. `git status` 检查是否有仅 `.meta` 变更但源文件未变更的项（异常）
2. `git status` 检查是否有新增源文件但 `.meta` 未暂存的情况（遗漏）
3. 确认 `.meta` 中 `guid` 不是 `00000000000000000000000000000000`

## 提交粒度约束

**以计划中的完整功能作为提交单元，而非单个任务。**

| 原则 | 说明 |
|------|------|
| 功能完整性 | 一次提交 = 一个完整的、可独立运行的功能 |
| 按功能划分 | 不同功能（如 HP 恢复 vs Buff 系统）分不同提交 |
| 不按步骤划分 | 同一功能的多个实现步骤合并为一个提交 |
| 可回滚性 | 回滚一个提交应该移除一个完整功能，而非半个功能 |

**示例**：
```
# ❌ 错误：按任务粒度提交
feat(character): 扩展 CharacterDefinition 支持 Buff
feat(effects): 创建 BuffConfig 数据模型
feat(effects): 创建 BuffEffectCalculator

# ✅ 正确：按功能粒度提交
feat(effects): 实现 Buff/Debuff 数据模型和效果计算器
```

**提交前提示用户检查**：
- 在执行 `git commit` 之前，必须先提示用户确认本次提交的内容
- 格式：`即将提交: <commit message>，包含 <N> 个文件变更。确认？`
- 用户确认后再执行提交

## 提交信息格式

```
<type>: <简短描述>

- <变更要点1>
- <变更要点2>
```

常用 type：`fix`（修复）、`feat`（新功能）、`refactor`（重构）

## 常见错误

- 新增 C# 脚本但忘记 `git add` 其 `.meta` → 其他人 checkout 后缺少 GUID 引用
- 删除 `.prefab` 但 `.meta` 残留 → 下次导入时生成新 GUID，引用断裂
- 多人冲突时 `.meta` 合并不当 → GUID 重复或丢失

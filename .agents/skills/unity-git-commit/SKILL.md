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

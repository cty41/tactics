---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot migration implementation
description: Unity frozen snapshot to Godot migration boundary, Core tests, adapters, and Poison Spear vertical slice.
tags: [migration, godot, core, testing]
timestamp: "2026-08-08T16:50:14+08:00"
status: active
catalog_scope: godot-migration
repo_paths:
  - Assets/Tactics/Shared/Core
  - Assets/Tactics/UnityAdapter
  - src/Tactics.Core
  - src/Tactics.Core.Tests
  - godot
  - Tests/golden
  - Tools/migration
verified_revision: 168d1934
source_fingerprint: sha256:ddb40b32ab32ba25e7e91fad4b90cff8629bb006e0d5f5d0bfe7890271622756
---

# Current state

迁移 worktree 已建立 `.NET 9` Core、NUnit 测试、Python 迁移转换器、Godot 4.7.1 Mono Adapter、GdUnit4Net 测试入口和 Poison Spear Lv1 的 ResourceSaver 生成器。Core 仍从 `Assets/Tactics/Shared/Core` 显式编译，保证 Unity 共存期只有一份源码。Poison Spear 首条垂直切片已登记技能、Presentation、10×10 fixture、Projectile、Impact 五个 ContentId，Godot Catalog 可编译并实例化两个 PackedScene，Presentation Resource 可转换并校验 Core `PresentationExecutionPlan`。

Godot 工程拓扑已收敛为单一正式工程：`D:\codes\tactics-worktrees\godot\godot\project.godot`。旧的 `godot-editor-spike` 临时 worktree/分支已退役；编辑器能力验证脚本统一为 `Tools/migration/Verify-GodotMigration.ps1`。

## Verification

- `Tactics.Core.Tests`：NUnit 10 项通过。
- `Tools/migration/tests`：10 项通过。
- `Tools/okf`：14 项通过。
- Godot C# adapter：`dotnet build` 通过。
- Godot headless runtime：Poison Spear ContentCatalog 五项、10×10 fixture、Core 结算、Presentation Plan 和 Projectile/Impact PackedScene 实例化均通过。
- Godot headless presentation：真实 Godot Tween 飞行、Impact 尾效和 `BattleRuntimeScope` 排空通过；ResourceSaver 生成六个目标文件重复执行后 SHA-256 不变。
- GdUnit4Net：3 项通过，包含纯 Core、Runtime marker 和 Poison Spear 资源闭环；Godot `--validate-poison-spear` 通过 `entries=5`、`damage=8`、`poisonTurns=3`。

## Pending manual gates

首次打开编辑器后的 C# Assembly Reload、GraphEdit marker 按钮、Undo/Redo、SubViewport 预览已由用户确认；Poison Spear SubViewport/运行时视觉验收仍需人工确认。Unity Adapter 的 Unity Editor 编译与 `.meta` 生成也尚未在 Unity Editor 中执行。

## Boundaries

`godot-ai` 保持外部 v3.1.2 只读基线，不进入 Core；TBSFramework、Odin 和 TilemapSet 不迁移。Windows Standalone 不属于迁移门禁。

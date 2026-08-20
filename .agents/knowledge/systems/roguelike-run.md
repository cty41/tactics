---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/src/Tactics.Core/Runs
title: Roguelike Run
description: Godot Pure Run 的七层路线、节点事务、队伍成长、存档和终局主链。
tags: [gameplay, roguelike, map, progression, godot]
timestamp: "2026-08-20T20:44:39+08:00"
status: active
catalog_scope: roguelike-run
repo_paths:
  - .agents/docs/2026-06-24-pure-run-squad-prototype-design.md
  - src/Tactics.Core/Runs
  - src/Tactics.Application/Runs
  - src/Tactics.Core.Tests/RunAdventureTransitionServiceTests.cs
  - src/Tactics.Application.Tests/RunSaveDocumentV9Tests.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotAdventureBoardView.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs
  - Tests/gameplay-specs/godot
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:58171b52450238cd227803b4de08fbf20965da23d170c963ca84a48e213bea43
---

# Current State

Pure Run 使用七层只前进路线、稳定节点 ID 和持久化 `RunAdventureState`。Application transition 统一移动、路线选择、节点事务、战斗请求/结算、成长、Inventory 与终局；成功事务使用稳定 key 防止重入时重复扣款、发奖或结算。

Godot Adventure Board 使用正式 Tile 投影和生产输入链呈现路线、队伍位置与节点状态。存档采用版本化文档、revision、hash、temp 重读与 backup 回退；损坏证据隔离保存，不静默覆盖。

当前可选角色、战斗、事件、休息、商店、宝箱、Elite、Boss、成长和终局均由代码、typed Resource 与测试共同定义。自动旅程验证逻辑和持久化边界；地图可读性、操作手感和视觉反馈由人工验收账本负责。

# Relationships

- 战斗节点使用 [Battle System](battle.md)。
- 技能成长与消耗品使用 [SkillGraph](skill-graph.md)。
- 固定 Seed 旅程由 [Gameplay Test Framework](gameplay-test-framework.md)验证。

# Verification Guidance

实现判断应核对 Run transition、存档 schema、Godot runtime、Resource/Catalog 与测试。必须覆盖事务重载、幂等、损坏回退和终局消费；截图不能替代玩家流验收。

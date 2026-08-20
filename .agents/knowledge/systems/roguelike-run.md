---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/src/Tactics.Core/Runs
title: Roguelike Run
description: Godot Pure Run 的七层路线、节点事务、队伍成长、存档和终局主链。
tags: [gameplay, roguelike, map, progression, godot]
timestamp: "2026-08-20T21:53:49+08:00"
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
source_fingerprint: sha256:5013a83ab4003fda349c0fdf48d5fe93ad46afd4a8c82c0ffcd9d385dc1a38a6
---

# Current State

Pure Run 使用七层只前进路线、稳定节点 ID 和持久化 `RunAdventureState`。每个节点的 Tile 场景只展示当前节点的直接后继出口；领队移动到相邻格并点击后立即选择目标，不存在开局两组路线预提交。Application transition 统一移动、即时出口、节点事务、战斗请求/结算、成长、Inventory 与终局；成功事务使用稳定 key 防止重入时重复扣款、发奖或结算。

Godot Adventure Board 使用正式 Tile 投影和生产输入链呈现出口、队伍位置与节点状态；Rogue Map 仅作只读总览。战斗节点先进入 Tile 场景，胜利后恢复同一 resolved 场景再开放出口。存档采用 V10、revision、hash、temp 重读与 backup 回退；V9 活跃 Run/Pending Setup 要求新局，Terminal Summary 保留，损坏证据隔离保存且不静默覆盖。

当前可选角色、战斗、事件、休息、商店、宝箱、Elite、Boss、成长和终局均由代码、typed Resource 与测试共同定义。自动旅程验证逻辑和持久化边界；地图可读性、操作手感和视觉反馈由人工验收账本负责。

# Relationships

- 战斗节点使用 [Battle System](battle.md)。
- 技能成长与消耗品使用 [SkillGraph](skill-graph.md)。
- 固定 Seed 旅程由 [Gameplay Test Framework](gameplay-test-framework.md)验证。

# Verification Guidance

实现判断应核对 Run transition、存档 schema、Godot runtime、Resource/Catalog 与测试。必须覆盖事务重载、幂等、损坏回退和终局消费；截图不能替代玩家流验收。

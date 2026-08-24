---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/src/Tactics.Core/Runs
title: Roguelike Run
description: Godot Pure Run 的七层路线、节点事务、队伍成长、存档和终局主链。
tags: [gameplay, roguelike, map, progression, godot]
timestamp: "2026-08-24T11:19:06+08:00"
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
source_fingerprint: sha256:ab3404d9311d35d435d461d68bab3f48b71f806377f7251acfd0f0e85c42a650
---

# Current State

Pure Run 使用七层只前进路线、稳定节点 ID 和持久化 `RunAdventureState`。每个节点的 Tile 场景只展示当前节点的直接后继出口；领队移动到相邻格并点击后立即选择目标，不存在开局两组路线预提交。Application transition 统一移动、即时出口、节点事务、战斗请求/结算、成长、Inventory 与终局；成功事务使用稳定 key 防止重入时重复扣款、发奖或结算。新地图合同增加 Planning、TacticalPreview、Current、Completed 情报状态，并用可选持久化字段保持旧 Save 编码稳定。

Godot Adventure Board 使用正式 Tile 投影和生产输入链呈现出口、队伍位置与节点状态。共享 `GodotIsometricTileMapSurface` 提供 10×10、96×48 的 Terrain/Decoration/Mask/Overlay TileMapLayer 与确定性拾取。应用启动不再经过 Home：可恢复状态直接 Continue，无有效存档直接创建 PendingRunSetup 并进入开始营地。营地选择按点击顺序逐人持久化，第一名为可移动领队，选择不可撤销；三人满员才解锁出口。Start 同时展示全路线的轻量 Planning Preview TileMap、连接和类型徽标，Preview 不承载 Actor、AI 或交互。Rogue Map 支持右键拖动、指针中心缩放、键盘平移、总览和聚焦；战斗节点共享表面仍由后续切片处理。

当前生产存档采用 V11、revision、payload hash、temp 重读、backup 回退与损坏文件隔离；兼容读取保留节点、领队、对象结果、发现、路线与遭遇 checkpoint，丢弃旧 ActorCells，并明确不保存相机、Tooltip、弹窗或逐行动战斗状态。Continue 因而从模板槽重建 Actor 与相机；PendingBattle 以已保存 Encounter、Seed 和入口 checkpoint 重开。

当前可选角色、战斗、事件、休息、商店、宝箱、Elite、Boss、成长和终局均由代码、typed Resource 与测试共同定义。自动旅程验证逻辑和持久化边界；地图可读性、操作手感和视觉反馈由人工验收账本负责。

# Relationships

- 战斗节点使用 [Battle System](battle.md)。
- 技能成长与消耗品使用 [SkillGraph](skill-graph.md)。
- 固定 Seed 旅程由 [Gameplay Test Framework](gameplay-test-framework.md)验证。

# Verification Guidance

实现判断应核对 Run transition、存档 schema、Godot runtime、Resource/Catalog 与测试。必须覆盖事务重载、幂等、损坏回退和终局消费；截图不能替代玩家流验收。

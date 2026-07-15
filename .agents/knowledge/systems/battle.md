---
type: Game System
resource: https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Common/Battle/BattleController.cs
title: Battle System
description: 棋盘战斗、回合、单位、结算和结构化战斗反馈的运行时主链。
tags: [gameplay, battle, turn-based, unity]
timestamp: "2026-07-15T00:06:58+08:00"
status: active
catalog_scope: battle-system
repo_paths:
  - .agents/plans/战斗系统演进计划.md
  - Assets/Tactics/Scripts/Common/Battle/BattleController.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs
  - Assets/Tactics/Scripts/Battle/BattleLog/TBattleLog.cs
  - Assets/Tactics/Tests/PlayMode/BattleControllerBattleUiBootstrapTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleLogConsoleTests.cs
verified_revision: d5f1730d3527
source_fingerprint: sha256:10239712858ede1cd47af7b3c6faa0ff2e4fece609e03fedca64487835e6702c
---

# Current State

`BattleController` 统一承接棋盘、玩家、单位、回合事件和战斗生命周期。`BattleSettlementCoordinator` 与 `BattleSettlementFlow` 负责奖励、最低等级单人成长、高级技能候选保底和结算 UI；`TBattleLog` 在战斗生命周期内收集结构化回合、技能、伤害、治疗和 Buff 信息。

`EncounterCatalog` 提供四类怪物定义、N1–N6/E1–E2/Special 显式配方、三类布局及普通/精英/Special 倍率，并由 resolver 产生稳定的 `ResolvedEncounter`。怪物定义携带明确 AbilityConfig 路径，动态生成时注入单位；该链路不使用运行时威胁预算。

# Relationships

- [SkillGraph](skill-graph.md)在战斗上下文中执行目标选择、位移和效果节点。
- [Monster AI](monster-ai.md)消费战斗快照并复用战斗执行器。
- [Roguelike Run](roguelike-run.md)发起战斗节点，并消费结算结果继续 run。

# Verification Guidance

战斗功能验收优先使用 PlayMode 测试和运行时数据。UI 日志显示验证应检查缓存、VisualElement 子节点和测试结果，不使用截图作为准确性依据。

# Citations

[1] [BattleController](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Scripts/Common/Battle/BattleController.cs)
[2] [TBattleLog](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Scripts/Battle/BattleLog/TBattleLog.cs)
[3] [BattleLogConsoleTests](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Tests/PlayMode/BattleLogConsoleTests.cs)

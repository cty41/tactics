---
type: Game System
resource: https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Common/Battle/BattleController.cs
title: Battle System
description: 棋盘战斗、属性、Buff、技能、结算和结构化战斗反馈的运行时主链。
tags: [gameplay, battle, turn-based, unity]
timestamp: "2026-07-17T20:23:36+08:00"
status: active
catalog_scope: battle-system
repo_paths:
  - .agents/docs/attribute-system-design.md
  - .agents/docs/buff-system-rules.md
  - .agents/docs/three-class-skill-design.md
  - Assets/Tactics/Scripts/Common/Battle/BattleController.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs
  - Assets/Tactics/Scripts/Common/Battle/FirstSliceSkillCatalog.cs
  - Assets/Tactics/Scripts/Battle/BattleLog/TBattleLog.cs
  - Assets/Tactics/Tests/PlayMode/BattleControllerBattleUiBootstrapTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleLogConsoleTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:7476716c4fe3e81b169e3ea7a7e7e7a1e1d5f6d89e01798c060ba427a732066a
---

# Current State

`BattleController` 承接棋盘、玩家、单位、回合事件和战斗生命周期。当前属性集合为力量、敏捷、体质、智力、魅力、幸运和速度；派生生命、法力、移动、先攻与恢复公式由当前属性文档和代码共同约束，不存在统一幸运暴击/闪避公式。

Buff 以配置引用和 CurseCategory 决定刷新/替换：同一配置重复施加会累加持续时间，同类别不同诅咒由后施加者替换；`BuffPolarity` 只区分 Beneficial/Harmful，净化统一移除 Harmful。标准正向 HP 恢复检查 `CanReceiveHealing`，复活类骷髅仍可选为目标但实际恢复为 0。三职业首批 18 技能已进入目录、SkillGraph 资产和测试。

`BattleSettlementCoordinator`/`BattleSettlementFlow` 负责战后成长和返回 Run；`TBattleLog` 收集结构化回合、技能、伤害、治疗和 Buff 信息。当前反馈已有伤害数字、Buff 图标与屏幕战斗日志。

Pure Run 战斗只把角色自己携带的独立实例注册成 `ConsumableBattleAbility`。战斗 UI 上排放移动与消耗品按钮，下排保持技能卡；药水可选择自身或正交相邻友军，每名角色每轮最多成功使用一次，且不占移动或普通技能机会。成功后立即提交实例消耗并保存。普通敌人与精英胜利分别按 25% 和 30% 概率从消耗品池掉落，掉落种子由 run seed 与节点 ID 推导；Boss 不追加掉落，因为其结算为终局。

# Relationships

- [SkillGraph](skill-graph.md)执行技能目标、位移和效果节点。
- [Monster AI](monster-ai.md)消费战斗快照并复用合法性与执行器。
- [Roguelike Run](roguelike-run.md)发起战斗并消费结算结果。
- 尚未激活的奖励、反馈和配置问题集中在[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

战斗规则优先由 PlayMode/Gameplay Test 验证。UI 日志检查缓存、VisualElement 子节点和测试结果，不使用截图作为准确性依据。

# Citations

[1] [BattleController](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Common/Battle/BattleController.cs)
[2] [FirstSliceSkillCatalog](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Common/Battle/FirstSliceSkillCatalog.cs)
[3] [BattleLogConsoleTests](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Tests/PlayMode/BattleLogConsoleTests.cs)

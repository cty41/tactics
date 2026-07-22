---
type: Game System
resource: https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Common/Battle/BattleController.cs
title: Battle System
description: 棋盘战斗、属性、Buff、技能、结算和结构化战斗反馈的运行时主链。
tags: [gameplay, battle, turn-based, unity]
timestamp: "2026-07-22T13:46:35+08:00"
status: active
catalog_scope: battle-system
repo_paths:
  - .agents/docs/attribute-system-design.md
  - .agents/docs/buff-system-rules.md
  - .agents/docs/three-class-skill-design.md
  - Assets/Tactics/Scripts/Common/Battle/BattleController.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs
  - Assets/Tactics/Scripts/Common/Battle/FirstSliceSkillCatalog.cs
  - Assets/Tactics/Scripts/Common/Battle/PureRunAbilityCatalog.cs
  - Assets/Tactics/Scripts/Common/Battle/PureRunAbilityBinder.cs
  - Assets/Tactics/Scripts/Battle/BattleLog/TBattleLog.cs
  - Assets/Tactics/Tests/Editor/PureRunAbilityCatalogEditorTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillCatalogTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleControllerBattleUiBootstrapTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleLogConsoleTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:87575c4304d37e90f8a2b3da448f07b3e921ee3d61fd35c2fd107d90923559a2
---

# Current State

`BattleController` 承接棋盘、玩家、单位、回合事件和战斗生命周期。当前属性集合为力量、敏捷、体质、智力、魅力、幸运和速度；派生生命、法力、移动、先攻与恢复公式由当前属性文档和代码共同约束，不存在统一幸运暴击/闪避公式。

Buff 以配置引用和 CurseCategory 决定刷新/替换：同一配置重复施加会累加持续时间，同类别不同诅咒由后施加者替换；`BuffPolarity` 只区分 Beneficial/Harmful，净化统一移除 Harmful。标准正向 HP 恢复检查 `CanReceiveHealing`，复活类骷髅仍可选为目标但实际恢复为 0。

`PureRunAbilityCatalog` 为三职业 18 个正式技能和隐藏额外技能 `amazon.pickup_spear` 提供稳定 ID、等级元数据与运行时资产解析。`PureRunAbilityBinder` 在玩家单位初始化前只注入职业普通攻击、实际已学主动技能和可解析的额外技能；被动按角色已学记录启用，Amazon 不再因职业身份在 Pure Run 中自动获得战斗技巧。缺少精确等级资产时仅向下回退并记录错误。当前发布的等级纵向闭环为火球术 Lv1/Lv2，其余职业等级资产仍由后续切片完成。

`BattleSettlementCoordinator`/`BattleSettlementFlow` 负责战后成长和返回 Run。Pure Run 升级候选从合法新技能 Lv1 与已学技能的下一个已发布等级组成确定性混合池；新技能受槽位限制，已学技能升级不占新槽。`TBattleLog` 收集结构化回合、技能、伤害、治疗和 Buff 信息。当前反馈已有伤害数字、Buff 图标与屏幕战斗日志。

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

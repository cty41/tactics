---
type: Game System
resource: https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Common/Battle/BattleController.cs
title: Battle System
description: 棋盘战斗、属性、Buff、技能、结算和结构化战斗反馈的运行时主链。
tags: [gameplay, battle, turn-based, unity]
timestamp: "2026-07-22T23:53:49+08:00"
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
  - Assets/Tactics/Scripts/Common/Battle/BattleInitiativeService.cs
  - Assets/Tactics/Scripts/Common/Battle/SummonRegistry.cs
  - Assets/Tactics/Scripts/Common/UnitSpeedTurnResolver.cs
  - Assets/Tactics/Scripts/Common/Units/DamageResolution.cs
  - Assets/Tactics/Scripts/Common/Units/FacingState.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/AbilityAvailability.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/MoveCommand.cs
  - Assets/Tactics/Tests/PlayMode/SharedBattlePrimitivesTests.cs
  - Assets/Tactics/Tests/PlayMode/MageSkillLevelTests.cs
  - Assets/Tactics/Scripts/Battle/BattleLog/TBattleLog.cs
  - Assets/Tactics/Tests/Editor/PureRunAbilityCatalogEditorTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillCatalogTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleControllerBattleUiBootstrapTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleLogConsoleTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:f50ef54e80612750e3d0e2b0fc7fa88d5d65d85c589ebf0605cc34c2077885b0
---

# Current State

`BattleController` 承接棋盘、玩家、单位、回合事件和战斗生命周期。当前属性集合为力量、敏捷、体质、智力、魅力、幸运和速度；派生生命、法力、移动、先攻与恢复公式由当前属性文档和代码共同约束，不存在统一幸运暴击/闪避公式。

伤害大类与元素是两个独立维度：`DamageCategory` 区分物理/魔法，`ElementType` 区分无元素、火、冰、水、土、风和电，允许无元素魔法伤害。直接伤害把命中、闪避、格挡和暴击结果写入 `DamageResolution`；只有显式设置 `RequiresSuccessfulHit` 的后续 Buff 节点才依赖同一目标的成功命中。

Buff 以标准状态类型、配置引用和 `CurseCategory` 决定刷新/替换。燃烧按层数累加且每次目标行动开始造成当前层数伤害后减 1；中毒固定每次施加增加 3 个行动周期、每周期固定伤害且伤害不叠加；减速固定 `Speed -2` 并刷新持续时间；眩晕固定跳过 1 次行动并刷新。标准状态即使来自不同配置也合并，其他 Buff 按配置的刷新策略处理，同类别不同诅咒由后施加者替换。`BuffPolarity` 只区分 Beneficial/Harmful，净化统一移除 Harmful。标准正向 HP 恢复检查 `CanReceiveHealing`，复活类骷髅仍可选为目标但实际恢复为 0。

地图待生效 Buff 可在单位初始化前挂载：`Unit` 先创建并保留 Buff 容器，战斗初始化时再绑定 `GridController`，不会清空恢复状态。战斗或回合切换取消 AI 延时属于正常生命周期，不记录错误日志。

单位持有四方向 `Facing` 状态。成功移动后按最后一步更新朝向，成功选择目标的技能按目标方向更新朝向，失败技能恢复原朝向；待输入状态下点击正交相邻格可免费转向。默认人类单位朝东、非人类单位朝西，表现层优先消费 Animator 的 `Facing`/`DirectionX`/`DirectionY` 参数，并为纯横向 Sprite 提供翻转回退。

`BattleInitiativeService` 按有效速度派生先攻并维护当前轮待行动顺序；减速等速度变化会立即重排尚未行动单位，不回滚已经行动的单位。`SummonRegistry` 按召唤者和类别记录召唤顺序，支持单体上限替换、原子批量替换和按召唤物已完成行动数计时；主动替换、到期、召唤者死亡与战斗结束会同步释放格子且不留下尸体。`AbilityAvailability` 统一表达可用、可点击禁用及隐藏状态，并携带稳定的禁用原因。

`PureRunAbilityCatalog` 为三职业 18 个正式技能和隐藏额外技能 `amazon.pickup_spear` 提供稳定 ID、等级元数据与运行时资产解析。`PureRunAbilityBinder` 在玩家单位初始化前只注入职业普通攻击、实际已学主动技能和可解析的额外技能；被动按角色已学记录启用，Amazon 不再因职业身份在 Pure Run 中自动获得战斗技巧。缺少精确等级资产时仅向下回退并记录错误。法师六项技能现已发布完整等级链：火球术、寒冰箭和霹雳闪电发布至 Lv3，召唤火魔、冰甲和瞬移术发布至 Lv2；其余职业等级资产由后续切片完成。

火魔是独立可治疗召唤物：生命 12、Speed/移动 4，使用 1–3 格火焰攻击并施加点燃；Lv2 召唤可在半径 3 内部分成功生成，重施法原子替换旧火魔。每只火魔在完成第 5 次自身行动后退场，跳过行动同样计数，战斗结束统一清理。

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

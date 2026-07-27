---
type: Game System
resource: https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Common/Battle/BattleController.cs
title: Battle System
description: 棋盘战斗、属性、Buff、技能、结算和结构化战斗反馈的运行时主链。
tags: [gameplay, battle, turn-based, unity]
timestamp: "2026-07-27T13:25:40+08:00"
status: active
catalog_scope: battle-system
repo_paths:
  - .agents/docs/attribute-system-design.md
  - .agents/docs/buff-system-rules.md
  - .agents/docs/three-class-skill-design.md
  - Assets/Tactics/Scripts/Common/Battle/BattleController.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleSettlementFlow.cs
  - Assets/Tactics/Scripts/Common/Battle/FirstSliceSkillCatalog.cs
  - Assets/Tactics/Scripts/Common/Battle/PureRunAbilityCatalog.cs
  - Assets/Tactics/Scripts/Common/Battle/PureRunAbilityBinder.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleInitiativeService.cs
  - Assets/Tactics/Scripts/Common/Battle/EncounterConfig.cs
  - Assets/Tactics/Scripts/Common/Battle/EncounterUnitRuntimeModifiers.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleRewardSystem.cs
  - Assets/Tactics/Scripts/Flow/Battle/BattleFlowCoordinator.cs
  - Assets/Tactics/Scripts/Common/Battle/AmazonBattleState.cs
  - Assets/Tactics/Scripts/Common/Battle/SummonRegistry.cs
  - Assets/Tactics/Scripts/Common/Interactables/DroppedSpear.cs
  - Assets/Tactics/Scripts/Common/UnitSpeedTurnResolver.cs
  - Assets/Tactics/Scripts/Common/Units/DamageResolution.cs
  - Assets/Tactics/Scripts/Common/Units/FacingState.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/AbilityAvailability.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/MoveCommand.cs
  - Assets/Tactics/Scripts/UI/BattleUIController.cs
  - Assets/Tactics/Arts/UI/Battle.uxml
  - Assets/Tactics/Arts/UI/Battle.uss
  - Assets/Tactics/Tests/PlayMode/SharedBattlePrimitivesTests.cs
  - Assets/Tactics/Tests/PlayMode/MageSkillLevelTests.cs
  - Assets/Tactics/Tests/PlayMode/NecromancerSkillLevelTests.cs
  - Assets/Tactics/Tests/PlayMode/AmazonSkillLevelTests.cs
  - Assets/Tactics/Scripts/Battle/BattleLog/TBattleLog.cs
  - Assets/Tactics/Tests/Editor/PureRunAbilityCatalogEditorTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillCatalogTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleControllerBattleUiBootstrapTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleLogConsoleTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:27b7fdd07a1478edb0a38d15493f0286419bb5c581fbd78804297a9ae6266b87
---

# Current State

`BattleController` 承接棋盘、玩家、单位、回合事件和战斗生命周期。当前属性集合为力量、敏捷、体质、智力、魅力、幸运和速度；派生生命、法力、移动、先攻与恢复公式由当前属性文档和代码共同约束，不存在统一幸运暴击/闪避公式。

伤害大类与元素是两个独立维度：`DamageCategory` 区分物理/魔法，`ElementType` 区分无元素、火、冰、水、土、风和电，允许无元素魔法伤害。直接伤害把命中、闪避、格挡和暴击结果写入 `DamageResolution`；只有显式设置 `RequiresSuccessfulHit` 的后续 Buff 节点才依赖同一目标的成功命中。

Buff 以标准状态类型、配置引用和 `CurseCategory` 决定刷新/替换。燃烧按层数累加且每次目标行动开始造成当前层数伤害后减 1；中毒固定每次施加增加 3 个行动周期、每周期固定伤害且伤害不叠加；减速固定 `Speed -2` 并刷新持续时间；眩晕固定跳过 1 次行动并刷新。标准状态即使来自不同配置也合并，其他 Buff 按配置的刷新策略处理，同类别不同诅咒由后施加者替换。`BuffPolarity` 只区分 Beneficial/Harmful，净化统一移除 Harmful。标准正向 HP 恢复检查 `CanReceiveHealing`，复活类骷髅仍可选为目标但实际恢复为 0。

地图待生效 Buff 可在单位初始化前挂载：`Unit` 先创建并保留 Buff 容器，战斗初始化时再绑定 `GridController`，不会清空恢复状态。战斗或回合切换取消 AI 延时属于正常生命周期，不记录错误日志。

正式 Mystery 事件使用真实 `BuffConfig` 资产传递跨战斗效果：诅咒宝箱的伤害承受提高 30% 与堕落祭坛的伤害减免 20% 均持续 3 个行动周期，并分别标记为 Harmful 与 Beneficial；资产通过 `GameAssetManager` 加载并由 PlayMode 测试校验精确效果字段。

单位持有四方向 `Facing` 状态。成功移动后按最后一步更新朝向，成功选择目标的技能按目标方向更新朝向，失败技能恢复原朝向；待输入状态下点击正交相邻格可免费转向。默认人类单位朝东、非人类单位朝西，表现层优先消费 Animator 的 `Facing`/`DirectionX`/`DirectionY` 参数，并为纯横向 Sprite 提供翻转回退。

`BattleInitiativeService` 按有效速度派生先攻并维护当前轮待行动顺序；减速等速度变化会立即重排尚未行动单位，不回滚已经行动的单位。Unit 按能力配置的稳定名称维护本回合成功使用次数，并在 `PrepareForTurn` 清空；共享 AbilityConfig 资产不会共享不同单位的运行时计数。`SummonRegistry` 按召唤者和类别记录召唤顺序，支持单体上限替换、原子批量替换和按召唤物已完成行动数计时；主动替换、到期、召唤者死亡与战斗结束会同步释放格子且不留下尸体。`AbilityAvailability` 统一表达可用、可点击禁用及隐藏状态，并携带稳定的禁用原因。

`PureRunAbilityCatalog` 为三职业 18 个正式技能和隐藏额外技能 `amazon.pickup_spear` 提供稳定 ID、等级元数据与运行时资产解析。`PureRunAbilityBinder` 在玩家单位初始化前只注入职业普通攻击、实际已学主动技能和可解析的额外技能；被动按角色已学记录启用，Amazon 不再因职业身份在 Pure Run 中自动获得战斗技巧。缺少精确等级资产时仅向下回退并记录错误。三职业等级资产均已按各技能设计上限连续发布。

火魔是独立可治疗召唤物：生命 12、Speed/移动 4，使用 1–3 格火焰攻击并施加点燃；Lv2 召唤可在半径 3 内部分成功生成，重施法原子替换旧火魔。每只火魔在完成第 5 次自身行动后退场，跳过行动同样计数，战斗结束统一清理。

死灵法师召唤严格选择并消耗一具尸体，骷髅战士与骷髅法师分别维护等级上限并最早替换；释放前找不到合法生成格时不消耗尸体、法力或旧召唤物。复活类召唤物不会产生新尸体，可被普通治疗选中但恢复结算为 0。伤害加深按等级扩展单体、十字和九宫格，骨矛支持首敌命中与直线穿透，骨盾重施法重置次数且 Lv2 可吸收全部战斗伤害。恐惧在目标下次行动开始时强制移动到离施法者最远的稳定可达格并消耗移动，随后仍可攻击或施法；重复施加刷新而不叠加。

亚马逊由 `AmazonBattleState` 维护每名角色唯一长矛、移动增伤和诱饵生命周期。突刺按等级延长直线并在 Lv3 消耗本回合实际移动格数形成无上限增伤；连续刺击按有序选择逐段独立暴击；毒矛命中后按等级扩散中毒并在半径 3 内确定性落矛，找不到合法落点时整次释放失败且不扣资源。落地长矛占格但不阻挡视线、不可受击；普通近战与持矛技能在落矛后以可点击禁用状态提示先回收，但移动保持可用。进入移动选择时，长矛格使用橙色引导，八方向可站立拾取位置使用绿色引导并覆盖普通青蓝移动高亮；引导不改变寻路或目标合法性。零消耗拾取要求八方向相邻，召唤回收可无视视线和阻挡。诱饵不进入先攻、不产生尸体或接受增益，敌方存在可达诱饵候选时只选择诱饵；战斗技巧共享一次闪避判定，并按等级追加一次非递归普攻或提高可暴击直接伤害的暴击率。

`BattleSettlementCoordinator`/`BattleSettlementFlow` 负责战后成长和返回 Run。Pure Run 升级候选从合法新技能 Lv1 与已学技能的下一个已发布等级组成确定性混合池；新技能受槽位限制，已学技能升级不占新槽。升级流程必须等待玩家同时选定属性与技能并显式确认，不再通过帧数超时自动推进；确认后先提交保底消费与成长状态，再统一保存。`TBattleLog` 收集结构化回合、技能、伤害、治疗和 Buff 信息。当前反馈已有伤害数字、Buff 图标与屏幕战斗日志。

BattleSettlement UI 在每次显示时重新解析当前 UIDocument 元素并重新注册继续/跳过动画回调，隐藏时释放旧树引用，避免跨战斗复用缓存实例时更新已经脱离面板的结算元素。

Pure Run 胜利结算只展示胜负、金币与总回合数。结算前先进入约 0.8 秒的不可交互恢复阶段：所有存活玩家单位按 `Constitution × 2` 恢复 HP、按 `Charisma` 恢复 MP（均受最大值限制），并分别显示绿色 HP 与蓝色 MP 浮字；死亡单位不恢复。恢复阶段隐藏战斗操作界面，之后才同步持久化状态并进入结算。

单位自身回合结束时恢复 `Intelligence` 点 MP（上限 `MaxMana`）；回合开始重置移动点、基础技能使用记录与能力的本回合成功使用计数，不再回蓝。实际恢复量为正时会同步捕获恢复者的世界坐标，Battle UI 以该快照显示蓝色 `+N MP` 浮字，不会被后续回合切换挪到下一名单位；已满 MP、零恢复或失效单位不显示，且浮字不阻塞回合切换。

Pure Run 战斗只把角色自己携带的独立实例注册成 `ConsumableBattleAbility`。战斗 UI 上排放移动与消耗品按钮，下排保持技能卡；药水可选择自身或正交相邻友军，每名角色每轮最多成功使用一次，且不占移动或普通技能机会。成功后立即提交实例消耗并保存。普通敌人与精英胜利分别按 25% 和 30% 概率从消耗品池掉落，掉落种子由 run seed 与节点 ID 推导；Boss 不追加掉落，因为其结算为终局。

Pure Run 遭遇将 E1/E2 的生命/输出倍率设为 1.3/1.15，Special 设为 1.8/1.25。生命倍率在派生属性完成后向上取整并满血出生；输出倍率在统一伤害入口消费，因此覆盖直接伤害和保留施法来源的持续伤害，不影响治疗、护盾与无来源环境效果。布局阻挡格在单位生成前占用，参与站立、寻路、落点和视线判断，并在战斗结束或控制器销毁时恢复原状态。配置加载会拒绝非法倍率、阻挡/出生重叠、缺失 Brain/Profile/能力、不可支付的已配置能力和 Pattern 悬空引用。

奖励入口先验证玩家方胜利；战败返回零金币、经验、物品和击杀统计。胜利只把带 `EncounterUnitRuntimeModifiers` 的正式敌方死亡计入 `enemiesDefeated`，召唤物、诱饵与测试对象不进入正式统计。

战斗技能卡统一消费 `AbilityAvailability`：隐藏技能不建卡，可点击禁用技能保留卡片并在点击后显示稳定原因。每张卡的回调捕获建卡角色和对应能力实例，执行前再次确认该角色仍被选中且仍持有该实例，避免角色/回合切换后按可变索引触发另一名角色的技能。连续刺击等有序多段技能显示当前段数和目标编号；右键或 Esc 每次撤销最后一段，队列为空时再次取消退出。落地长矛以不参与点击和视线判断的独立世界标记显示。

当前战斗原始数值、遭遇倍率和实际伤害顺序的审计基线见 `.agents/docs/pure-run-current-combat-values.md`；该文不改变任何运行时数值。

Pure Run 正式战斗会在单位管理器初始化前生成队伍与遭遇，并为所有实际出现的阵营补齐玩家控制器；玩家出生格优先选择相机可见、可行走且未占用的配置或最近合法格。战斗 Camera 在初始化、单位选择和回合切换期间保持固定，Battle UI 只读取 Camera 做世界标记投影；右键优先取消目标选择而不打开 Pause。战斗返回直接以 Single 模式原子加载目标场景，不先卸载唯一的 Battle 场景。同步致死可能立即销毁单位，伤害日志、受击事件、Buff 回调、AI 和 UI 都会先验证 Unity 对象仍有效，避免战斗结束帧访问已销毁目标。

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

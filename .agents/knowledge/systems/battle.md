---
type: Game System
resource: https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Common/Battle/BattleController.cs
title: Battle System
description: 棋盘战斗、属性、Buff、技能、结算和结构化战斗反馈的运行时主链。
tags: [gameplay, battle, turn-based, unity]
timestamp: "2026-08-05T12:04:50+08:00"
status: active
catalog_scope: battle-system
repo_paths:
  - .agents/docs/attribute-system-design.md
  - .agents/docs/battle-facing-rules.md
  - .agents/docs/buff-system-rules.md
  - .agents/docs/three-class-skill-design.md
  - Assets/Tactics/Scripts/Common/Battle/BattleController.cs
  - Assets/Tactics/Scripts/Common/Battle/Runtime/BattleRuntimeScope.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleSettlementFlow.cs
  - Assets/Tactics/Scripts/Common/Battle/FirstSliceSkillCatalog.cs
  - Assets/Tactics/Scripts/Common/Battle/PureRunAbilityCatalog.cs
  - Assets/Tactics/Scripts/Common/Battle/PureRunAbilityBinder.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleInitiativeService.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleBackdropFitter.cs
  - Assets/Tactics/Scripts/Common/Battle/EncounterConfig.cs
  - Assets/Tactics/Scripts/Common/Battle/EncounterUnitRuntimeModifiers.cs
  - Assets/Tactics/Scripts/Common/Battle/BattleRewardSystem.cs
  - Assets/Tactics/Scripts/Flow/Battle/BattleFlowCoordinator.cs
  - Assets/Tactics/Scripts/Common/Battle/AmazonBattleState.cs
  - Assets/Tactics/Scripts/Common/Battle/SummonRegistry.cs
  - Assets/Tactics/Scripts/Common/Interactables/DroppedSpear.cs
  - Assets/Tactics/Scripts/Common/UnitSpeedTurnResolver.cs
  - Assets/Tactics/Scripts/Common/GamePauseService.cs
  - Assets/Tactics/Scripts/Common/GameTimeService.cs
  - Assets/Tactics/Scripts/Common/GameTimeService.cs.meta
  - Assets/Tactics/Scripts/Common/players/TurnSkipHelper.cs
  - Assets/Tactics/Scripts/Common/Units/DamageResolution.cs
  - Assets/Tactics/Scripts/Common/Units/TilemapUnit.cs
  - Assets/Tactics/Scripts/Common/Cells/TilemapCellManager.cs
  - Assets/Tactics/Scripts/Common/Cells/ProceduralTileHighlightRenderer.cs
  - Assets/Tactics/Scripts/Common/Units/FacingState.cs
  - Assets/Tactics/Scripts/Common/Units/FacingCoordinator.cs
  - Assets/Tactics/Scripts/Common/Units/Tween
  - Assets/Tactics/Scripts/Common/Interactables/Corpse.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/AbilityAvailability.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/MoveCommand.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/UnityMoveComponent.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/Executors/MovementAndEffectExecutors.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/BattlePresentationCoordinator.cs
  - Assets/Tactics/Scripts/UI/BattleUIController.cs
  - Assets/Tactics/Arts/UI/Battle.uxml
  - Assets/Tactics/Arts/UI/Battle.uss
  - Assets/Tactics/Arts/Materials/BattleBackdrop.mat
  - Assets/Tactics/Arts/Prefabs/BattleBackdrop.prefab
  - Assets/Tactics/Shaders/BattleBackdrop.shader
  - Assets/Tactics/Scenes/Test1.unity
  - Assets/Tactics/Tests/Editor/Test1BattleMapLayoutEditorTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleBackdropFitterTests.cs
  - Assets/Tactics/Tests/PlayMode/SharedBattlePrimitivesTests.cs
  - Assets/Tactics/Tests/PlayMode/FacingBehaviorPlayModeTests.cs
  - Assets/Tactics/Tests/PlayMode/MageSkillLevelTests.cs
  - Assets/Tactics/Tests/PlayMode/NecromancerSkillLevelTests.cs
  - Assets/Tactics/Tests/PlayMode/AmazonSkillLevelTests.cs
  - Assets/Tactics/Scripts/Battle/BattleLog/TBattleLog.cs
  - Assets/Tactics/Tests/Editor/PureRunAbilityCatalogEditorTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillCatalogTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleControllerBattleUiBootstrapTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleLogConsoleTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleSpeedControlUiTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleSpeedControlUiTests.cs.meta
  - Assets/Tactics/Tests/PlayMode/GameTimeServiceSpeedTests.cs
  - Assets/Tactics/Tests/PlayMode/GameTimeServiceSpeedTests.cs.meta
  - Assets/Tactics/Tests/Editor/PureRunTweenAssetTests.cs
  - Assets/Tactics/Tests/PlayMode/PureRunTweenPlayModeTests.cs
  - Assets/Tactics/Tests/PlayMode/BattleRuntimeScopePlayModeTests.cs
  - Assets/Tactics/Tests/Editor/BattleRuntimeScopeApiContractTests.cs
  - Assets/Tactics/Arts/PureRun/Tween
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:3fc4c4df4d092447e0859105219394136ea97a2e32edace9c5007a9835babe8e
---

# Current State

`BattleController` 承接棋盘、玩家、单位、回合事件和战斗生命周期。当前属性集合为力量、敏捷、体质、智力、魅力、幸运和速度；派生生命、法力、移动、先攻与恢复公式由当前属性文档和代码共同约束，不存在统一幸运暴击/闪避公式。

伤害大类与元素是两个独立维度：`DamageCategory` 区分物理/魔法，`ElementType` 区分无元素、火、冰、水、土、风和电，允许无元素魔法伤害。直接伤害把命中、闪避、格挡和暴击结果写入 `DamageResolution`；只有显式设置 `RequiresSuccessfulHit` 的后续 Buff 节点才依赖同一目标的成功命中。

Buff 以标准状态类型、配置引用和 `CurseCategory` 决定刷新/替换。燃烧按层数累加且每次目标行动开始造成当前层数伤害后减 1；中毒固定每次施加增加 3 个行动周期、每周期固定伤害且伤害不叠加；减速固定 `Speed -2` 并刷新持续时间；眩晕固定跳过 1 次行动并刷新。标准状态即使来自不同配置也合并，其他 Buff 按配置的刷新策略处理，同类别不同诅咒由后施加者替换。`BuffPolarity` 只区分 Beneficial/Harmful，净化统一移除 Harmful。标准正向 HP 恢复检查 `CanReceiveHealing`，复活类骷髅仍可选为目标但实际恢复为 0。

地图待生效 Buff 可在单位初始化前挂载：`Unit` 先创建并保留 Buff 容器，战斗初始化时再绑定 `GridController`，不会清空恢复状态。战斗或回合切换取消 AI 延时属于正常生命周期，不记录错误日志。

正式 Mystery 事件使用真实 `BuffConfig` 资产传递跨战斗效果：诅咒宝箱的伤害承受提高 30% 与堕落祭坛的伤害减免 20% 均持续 3 个行动周期，并分别标记为 Harmful 与 Beneficial；资产通过 `GameAssetManager` 加载并由 PlayMode 测试校验精确效果字段。

单位持有四方向 `Facing` 状态，坐标解析由 `FacingResolver` 保持纯计算，行为更新统一收束到 `FacingCoordinator`。普通玩家/AI 移动、冲锋者与 Dash 施法者在每个新路径段开始前转向；恐惧在逃跑换格前转向；冲锋退让目标、击退、抛飞及受击保持原朝向。技能选择期间，悬停单位或格子都会预览施法者朝向，移动技能优先按可达路径第一段预览，取消或失败保留最后预览；有序多目标技能的合法锥形继续使用进入选择时锁定的方向。待输入状态下点击正交相邻格仍可免费转向。默认人类单位朝东、非人类单位朝西，表现层优先消费 Animator 的 `Facing`/`DirectionX`/`DirectionY` 参数，并为纯横向 Sprite 提供翻转回退。完整规则见 `.agents/docs/battle-facing-rules.md`。

`BattleInitiativeService` 按有效速度派生先攻并维护当前轮待行动顺序；减速等速度变化会立即重排尚未行动单位，不回滚已经行动的单位。Unit 按能力配置的稳定名称维护本回合成功使用次数，并在 `PrepareForTurn` 清空；共享 AbilityConfig 资产不会共享不同单位的运行时计数。`SummonRegistry` 按召唤者和类别记录召唤顺序，支持单体上限替换、原子批量替换和按召唤物已完成行动数计时；主动替换、到期、召唤者死亡与战斗结束会同步释放格子且不留下尸体。`AbilityAvailability` 统一表达可用、可点击禁用及隐藏状态，并携带稳定的禁用原因。

普通非召唤单位死亡当帧即从 `UnitManager` 和原 Cell 的活体列表移除，并在原 Cell 注册可选中、占格且可被死灵技能消耗的 `Corpse`；这些玩法语义不等待表现。Pure Run 单位有专用死亡 Sprite 和标准 Tween 时，尸体先应用 Sprite、材质、Tint 及死者主 Renderer 当时的 Sorting Layer/Order，但隐藏 Renderer；活体进入 `Dying`，依次播放 `0.07s` recoil、`0.05s` shake 与 `0.08s` collapse，在 `0.20s` 幂等 Handoff，隐藏活体全部 Sprite/Shadow、显示尸体并开始 `0.13s` 下落、`0.07s` 冲击压缩与 `0.08s` 回弹。Handoff 和落地不再改变尸体排序，缺少来源 Renderer 的兼容单位保留尸体 Prefab 排序。collapse 末帧回到 Tile 基准和基础旋转，并从底部 Pivot 压到 `1.02× / 0.58×`，避免 Hit Sprite 直接跳为扁平尸体。取消、Disable、Destroy 或缺少攻击来源时立即执行同一 Handoff，不能留下永久隐藏的尸体。正式死亡图已按 Alpha AABB 居中并使用零局部偏移；未配置完整新链路的旧单位继续使用原销毁回退，召唤物与诱饵继续不生成尸体。

标准地面 Pure Run 单位通过共享 `StandardUnitTweenProfile` 与 `UnitTweenVisual` 表现 Idle、逐路径段移动、近战、远程、施法和受击。内部生命周期为 `Alive → Dying → Removed`：Alive 内继续用优先级协调动作；Dying 终止性抢占 Move、Action 与普通 Hit，并通过 generation/version 和幂等 Handoff 阻止旧 Tween 回调恢复 Idle 或重复交接；Removed 不再启动 Tween。方向、技能、VFX 和姿态族仍作为状态载荷，不扩张生命周期枚举。`UnitPoseFamily` 为单帧姿态声明 Release/恢复段退出语义，`UnitActionPoseProfile` 把角色默认动作族、`Default/Unarmed` 状态与双原生方向图分开配置；显式缺图只回退同族默认状态或 idle，不借用无关姿态，表现缺失不影响图执行。Cast 开始时仍以施法者 Sprite 中心发送非阻塞 `CastCharge`；允许 Profile 配置化切换人物 Sprite，但禁止复制整人物 Overlay，并保持主 Renderer 的 Material、Color、Sorting、Shadow 和 Transform 契约。赤柴 Cast 与 Hit 的 `Default / Unarmed` 分别显式共用各自的一对无矛方向图；姿态期间只隐藏 Sprite 内长矛而不改 `IsSpearHeld`，恢复段按权威状态回到对应 idle。法师与死灵法师分别使用只含 Default Cast/Hit 的独立 Profile，Melee、Ranged 与 Idle 覆盖为空，未来换图不改变动作族和时序接口。尸体继续使用独立死亡 Sprite，不继承动作姿态或镜像。蝙蝠等飞行单位暂不接入。

`UnitTweenVisual` 的状态不序列化；Play Mode Inspector 通过 internal 快照只读显示 Lifecycle、Foreground Priority、Idle/Move/Foreground Tween 活跃状态、Foreground Version 与 Death Handoff，自动刷新但不能强制切状态或写回资产。

Tween 的长期责任限定为简单且可复用的视觉运动：角色姿态、移动、受击、攻击后坐、施法准备和投射物位移。低复杂度光环、闪光、短尾迹和颜色脉冲仍可使用程序化原语；复杂技能的核心美术表现不再以扩充 Tween/有限原语为默认路径。火球、骨矛和突刺已采用 Piloto 项目侧粒子与程序化接触骨架混合，Recipe 保留 Marker、多命中位置和缺资产回退，但不再承担主要画面。

`Tactics/Pure Run/Presentation Graph Editor` 将角色 Tween、投射物、第三方 Prefab FX 和程序化 Recipe 作为同一纯表现图的叶节点预览；Graph 本身不写伤害、Buff 或目标状态。18 个正式图通过 Editor-only Preview Scenario 播放代表性完整技能，按 Release、Projectile Impact、程序化 Blocking 或 Track 完成推进 Phase，同时保留动作恢复与视觉尾段重叠。目标受击使用目标自己的 Hit Family，伤害加深不模拟伤害受击；突刺完整顺序为动作 Release、程序化接触骨架与定向 Piloto 枪芒并行、粒子命中爆点和目标受击。Preview 无手动 Entry 覆盖，旧图才回退 `DefaultPreviewEntry`。`SourceToTarget` 粒子在 Preview 与 Runtime 共享旋转/距离伸展公式；隔离舞台支持方向、距离、倍速、时间拖动和固定种子重建，Stop、资源切换、窗口关闭和程序集重载会恢复 Sprite/Transform/Shadow 并清理临时对象；Scenario 不参与 Runtime 或玩法结算。

技能接触反馈由可选 `SkillVfxRecipe` 驱动。执行器在伤害前保存世界坐标，只发送强类型 Cue；Coordinator 只等待释放/接触关键帧，淡出、粒子和残影非阻塞。投射物抵达时先完成 `ProjectileImpact` 接触点再写入命中黑板；骨矛使用独立中心 Sprite、切线旋转与最多两个短残影，并在取消时同步清理。Sprite 投射物未显式配置 Material 时保留 `SpriteRenderer` 默认材质，残影遵循同一规则，不会用空材质触发洋红错误 Shader。实际伤害仍以 `DamageResolution.WasHit` 决定次目标/命中反馈，表现缺失或取消不能改变玩法结果。突刺方向端点不因射线上先命中的敌人被通用 LOS 隐藏，但扫描仍在友军、永久地形和非法格处结束。

`PureRunAbilityCatalog` 为三职业 18 个正式技能和隐藏额外技能 `amazon.pickup_spear` 提供稳定 ID、等级元数据与运行时资产解析。`PureRunAbilityBinder` 在玩家单位初始化前只注入职业普通攻击、实际已学主动技能和可解析的额外技能；被动按角色已学记录启用，Amazon 不再因职业身份在 Pure Run 中自动获得战斗技巧。缺少精确等级资产时仅向下回退并记录错误。三职业等级资产均已按各技能设计上限连续发布。

三职业首批完整 VFX 垂直样本继续由 Presentation Graph 收束：毒矛由 `Action/Release` 与 `Projectile/Impact` 两个入口保证到达后才进入中毒和实体落矛结算；霹雳闪电通过 `PlayPresentationCue` 请求目标命中 Prefab FX。伤害加深诅咒使用闭合三分支 Fork/Join 同时播放目标脚下的地面双环法阵、后层远侧火焰和前层近侧火焰；八个主火柱以可见根部锚定外环并按顺时针依次点燃，三层均为 FireAndForget，粒子寿命不并入 Buff 结算时长。

第二批突刺、火球和骨矛的全部已发布等级也已统一到 Presentation Graph，但叶资产仍是临时程序化视觉基线。9 个正式图成为 12 个 Ability 消费端的唯一表现入口，顶层 legacy 动作/Recipe 与玩法图 projectile Profile 均清空；图内继续复用既有 Recipe、Fire/BoneSpear Profile、速度与接触时序。内部 Cue 快照会把路径、多个实际命中点、主命中点和强度完整透传到程序化节点，表现层不写伤害、Buff、目标或资源消耗。

`BattleController` 每场创建并独占替换一个 `BattleRuntimeScope`，外部只能读取、不能公开设置；启动期 UI、FireAndForget cue 和 projectile impact 都注册到该 scope。结束、返回和场景切换先取消、等待 tracked drain，再释放 scope；`OnDestroy` fallback 同步取得 teardown task 和已加载路径快照，并仅在 drain 完成后释放这些资产，不让异步 continuation 访问已销毁组件。teardown 即使观察到 tracked fault 也会完成资源释放并保持既有结束事件流程，同时通过 `RuntimeScopeTeardownException` 显式暴露非取消异常，不依赖日志策略判断成功。并发完成、timeout、取消回调重入 Dispose、pending start、replacement scope、faulted task 观察和回调异常边界由 PlayMode 回归约束，已完成任务的异常不能在池回收或 teardown 中静默丢失。

火魔是独立可治疗召唤物：生命 12、Speed/移动 4，使用 1–3 格火焰攻击并施加点燃；Lv2 召唤可在半径 3 内部分成功生成，重施法原子替换旧火魔。每只火魔在完成第 5 次自身行动后退场，跳过行动同样计数，战斗结束统一清理。

死灵法师召唤严格选择并消耗一具尸体，骷髅战士与骷髅法师分别维护等级上限并最早替换；释放前找不到合法生成格时不消耗尸体、法力或旧召唤物。复活类召唤物不会产生新尸体，可被普通治疗选中但恢复结算为 0。伤害加深按等级扩展单体、十字和九宫格，骨矛支持首敌命中与直线穿透，骨盾重施法重置次数且 Lv2 可吸收全部战斗伤害。恐惧在目标下次行动开始时强制移动到离施法者最远的稳定可达格并消耗移动，随后仍可攻击或施法；重复施加刷新而不叠加。

亚马逊由 `AmazonBattleState` 维护每名角色唯一长矛、移动增伤和诱饵生命周期。突刺按等级延长直线并在 Lv3 消耗本回合实际移动格数形成无上限增伤；连续刺击按有序选择逐段独立暴击；毒矛命中后按等级扩散中毒并在半径 3 内确定性落矛，找不到合法落点时整次释放失败且不扣资源。毒矛 Release 的表现顺序固定为切换 `Unarmed`、清除投掷姿态、再启动技能图；Presentation Graph 与兼容 Tween 路径共用同一 Release 准备回调，成功、失败或取消后按 `IsSpearHeld` 对账。实体落矛与缓存重建保持空手，远程召回、相邻免费拾取和战斗清理恢复持矛；该视觉投影不改变近战/持矛技能的既有禁用规则。落地长矛占格但不阻挡视线、不可受击；移动选择、回收、诱饵和战斗技巧规则保持不变。

`BattleSettlementCoordinator`/`BattleSettlementFlow` 负责战后成长和返回 Run。Pure Run 升级候选从合法新技能 Lv1 与已学技能的下一个已发布等级组成确定性混合池；新技能受槽位限制，已学技能升级不占新槽。升级流程必须等待玩家同时选定属性与技能并显式确认，不再通过帧数超时自动推进；确认后先提交保底消费与成长状态，再统一保存。`TBattleLog` 收集结构化回合、技能、伤害、治疗和 Buff 信息。当前反馈已有伤害数字、Buff 图标与屏幕战斗日志。

BattleSettlement UI 在每次显示时重新解析当前 UIDocument 元素并重新注册继续/跳过动画回调，隐藏时释放旧树引用，避免跨战斗复用缓存实例时更新已经脱离面板的结算元素。

`GameTimeService` 是 production 中 `Time.timeScale`、当前进程播放倍率和嵌套暂停深度的唯一所有者；支持固定 `1× → 2× → 4× → 0.5× → 1×` 循环，暂停期间只更新恢复目标，最后一层 Resume 才恢复所选倍率。倍率跨场景和后续战斗保留，Unity subsystem 初始化时重置为 `1×` 且未暂停；`GamePauseService` 仅保留兼容转发。Battle UI 右上角速度按钮直接读取服务的显式浮点倍率，缓存重进时重新接线且不复制倍率状态。游戏世界异步等待统一使用可取消、pause-aware 的 scaled delay；战后恢复等待绑定 Battle UI 销毁令牌，异常退出不会留下暂停中的悬挂任务。测试 timeout、AI deadlock ceiling、资源加载保护等基础设施 deadline 继续使用 realtime。

Pure Run 胜利结算只展示胜负、金币与总回合数。结算前先进入约 0.8 秒的不可交互恢复阶段：所有存活玩家单位按 `Constitution × 2` 恢复 HP、按 `Charisma` 恢复 MP（均受最大值限制），并分别显示绿色 HP 与蓝色 MP 浮字；死亡单位不恢复。恢复阶段隐藏战斗操作界面，之后才同步持久化状态并进入结算。

单位自身回合结束时恢复 `Intelligence` 点 MP（上限 `MaxMana`）；回合开始重置移动点、基础技能使用记录与能力的本回合成功使用计数，不再回蓝。实际恢复量为正时会同步捕获恢复者的世界坐标，Battle UI 以该快照显示蓝色 `+N MP` 浮字，不会被后续回合切换挪到下一名单位；已满 MP、零恢复或失效单位不显示，且浮字不阻塞回合切换。

Pure Run 战斗只把角色自己携带的独立实例注册成 `ConsumableBattleAbility`。战斗 UI 上排放移动与消耗品按钮，下排保持技能卡；药水可选择自身或正交相邻友军，每名角色每轮最多成功使用一次，且不占移动或普通技能机会。成功后立即提交实例消耗并保存。普通敌人与精英胜利分别按 25% 和 30% 概率从消耗品池掉落，掉落种子由 run seed 与节点 ID 推导；Boss 不追加掉落，因为其结算为终局。

Pure Run 遭遇将 E1/E2 的生命/输出倍率设为 1.3/1.15，Special 设为 1.8/1.25。生命倍率在派生属性完成后向上取整并满血出生；输出倍率在统一伤害入口消费，因此覆盖直接伤害和保留施法来源的持续伤害，不影响治疗、护盾与无来源环境效果。布局阻挡格在单位生成前占用，参与站立、寻路、落点和视线判断，并在战斗结束或控制器销毁时恢复原状态。配置加载会拒绝非法倍率、阻挡/出生重叠、缺失 Brain/Profile/能力、不可支付的已配置能力和 Pattern 悬空引用。

奖励入口先验证玩家方胜利；战败返回零金币、经验、物品和击杀统计。胜利只把带 `EncounterUnitRuntimeModifiers` 的正式敌方死亡计入 `enemiesDefeated`，召唤物、诱饵与测试对象不进入正式统计。

战斗技能卡统一消费 `AbilityAvailability`：隐藏技能不建卡，可点击禁用技能保留卡片并在点击后显示稳定原因。每张卡的回调捕获建卡角色和对应能力实例，执行前再次确认该角色仍被选中且仍持有该实例，避免角色/回合切换后按可变索引触发另一名角色的技能。连续刺击等有序多段技能显示当前段数和目标编号；右键或 Esc 每次撤销最后一段，队列为空时再次取消退出。落地长矛以不参与点击和视线判断的独立世界标记显示。

当前战斗原始数值、遭遇倍率和实际伤害顺序的审计基线见 `.agents/docs/pure-run-current-combat-values.md`；该文不改变任何运行时数值。

Pure Run 单位状态反馈绑定到单位根：待命、选中、已行动和可攻击状态分别绘制低饱和蓝灰、柔和琥珀、弱灰蓝和暖红等距 Tile 面，不再在角色身上显示方形 Marker。单位根与 `ICell.WorldPosition` 在静止时统一表示 Ground Center；Pure Run Sprite/VisualRoot 使用 Prefab 零基线，Shadow 作者基线为根空间 `localY=-0.03`，运行时不再二次改写 Sprite 或 Shadow。

鼠标格子拾取先以战场平面 Raycast 得到精确世界坐标，再通过 `TilemapCellGeometry.WorldToCell` 直接解析；禁止额外叠加 `GetCellCenterWorld - CellToWorld` 半格补偿。`CellToWorld` 只表示 Cell Origin，`TilemapCellGeometry.GetGroundCenterWorld` 才是单位、尸体、高亮、Scene Handle 和自动化点击共用的地面落点。高亮从相邻 Ground Center 构造菱形，不再用世界 Y 偏移处理渲染层级。完整契约见 `.agents/docs/isometric-grid-anchor-contract.md`。

高亮 Renderer 将锁定逻辑格的 Hover、范围、路径、AoE 与技能引导，和绑定单位根的 Friendly、Selected、Finished、Targetable 分为两个 Mesh。单位状态在 `LateUpdate` 连续跟随根节点，禁止由 `UnitLeftCell`、`CurrentCell` 或 `WorldToCell` 驱动，因此逻辑格提前切换不会再让脚底高亮抢先跳格，Sprite/VisualRoot 的局部 Tween 也不会影响地面位置。`PlayerNumber == 0` 固定为友方；非 0 阵营不显示 Friendly、Selected、Finished，但 Targetable 红色提示仍可见。

场景卸载时 CellManager、高亮 Renderer 与单位的销毁顺序不固定。`TilemapUnit` 清理单位状态高亮前会使用 Unity 对象有效性判断；`TilemapCellManager` 的移除操作只消费仍存在的 Renderer，不在销毁阶段懒加载或重建渲染组件。该清理是 best-effort，本地高亮状态无论 Manager 是否已销毁都会复位。

Pure Run 正式战斗会在单位管理器初始化前生成队伍与遭遇，并为所有实际出现的阵营补齐玩家控制器；玩家出生格优先选择相机可见、可行走且未占用的配置或最近合法格。战斗开始只自动打开 Battle UI，不再默认创建或显示 Cheat Console；调试控制台继续由 ToggleConsole 输入显式打开，并在战斗结束时关闭。战斗 Camera 在初始化、单位选择和回合切换期间保持固定，Battle UI 只读取 Camera 做世界标记投影；右键优先取消目标选择而不打开 Pause。战斗返回直接以 Single 模式原子加载目标场景，不先卸载唯一的 Battle 场景。同步致死可能立即销毁单位，伤害日志、受击事件、Buff 回调、AI 和 UI 都会先验证 Unity 对象仍有效，避免战斗结束帧访问已销毁目标。

战斗场景通过单个 `BattleBackdrop` Prefab 提供静态深蓝渐变背景。Prefab 序列化引用 URP Unlit 材质和 Quad 网格，`BattleBackdropFitter` 在正交相机的尺寸、宽高比或位姿变化后以 2% overscan 重新铺满视口；相机缺失或为透视模式时隐藏背景并仅警告一次。当前 `Test1` 已接入该 Prefab，新战斗场景沿用同一单实例规则，不由战斗流程动态创建。

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

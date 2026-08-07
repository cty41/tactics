---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Skills/Graph
title: SkillGraph
description: 技能资产、解释器、Ability 桥接、共享目标规则和 Agent-first 创作验证主链。
tags: [gameplay, skills, skill-graph, unity]
timestamp: "2026-08-07T19:15:46+08:00"
status: active
catalog_scope: skill-graph
repo_paths:
  - .agents/docs/skill-graph-system.md
  - .agents/skills/skill-graph-creation/SKILL.md
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphAsset.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/ProjectileVisualProfile.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/ProjectileVisualCoordinator.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/ProjectileTweenBuilder.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillVfxRecipe.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillVfxCoordinator.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillVfxPrimitiveBuilder.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/BattlePresentationGraph.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/BattlePresentationGraphValidation.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/BattlePresentationCoordinator.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/TransientVfxPool.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/VisualCueProfile.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/VisualCueCoordinator.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRunner.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphSpec.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillTargetingProtocol.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/OrderedTargetSelectionState.cs
  - Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphSpecCompiler.cs
  - Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphAssetMenu.cs
  - Assets/Tactics/Tests/Editor/SkillGraphAssetMenuEditorTests.cs
  - Assets/Tactics/Scripts/Editor/PresentationWorkbench
  - Assets/Tactics/Scripts/Editor/PresentationGraph
  - Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/AbilityConfig.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityImpl.cs
  - Assets/Tactics/Battle/Abilities/SkillGraphs
  - Assets/Tactics/Arts/PureRun/Presentation
  - Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/ChargeStrike_Lv1_Ability.asset
  - Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/AreaBlast_Lv1_Ability.asset
  - Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/HeavyShot_Graph_Ability.asset
  - Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv1_Ability.asset
  - Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv2_Ability.asset
  - Assets/Tactics/Tests/PlayMode/SkillGraphRuntimeTests.cs
  - Assets/Tactics/Tests/PlayMode/FacingBehaviorPlayModeTests.cs
  - Assets/Tactics/Tests/PlayMode/SkillAbilityUsesPerTurnTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs
  - Assets/Tactics/Tests/PlayMode/MageSkillLevelTests.cs
  - Assets/Tactics/Tests/PlayMode/NecromancerSkillLevelTests.cs
  - Assets/Tactics/Tests/Editor/PureRunTweenAssetTests.cs
  - Assets/Tactics/Tests/PlayMode/PureRunTweenPlayModeTests.cs
  - Assets/Tactics/Tests/PlayMode/TransientVfxLifecyclePlayModeTests.cs
  - Assets/Tactics/Tests/PlayMode/PilotoVfxPerformancePlayModeTests.cs
  - Assets/Tactics/Tests/Editor/PilotoVfxSampleAssetTests.cs
  - Assets/Tactics/Tests/Editor/BattlePresentationGraphEditorTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:bb48d556cc8faccf19f828eab9d60b1959e062371855dc45cc8178f789681a0a
---

# Current State

`SkillGraphAsset` 保存编辑态节点图，`SkillGraphRunner` 解释执行，`SkillGraphAbilityImpl` 接入既有 `IAbility`、共享 targeting 和计划执行接口。玩家预览、AI 候选及执行前重验证复用射程、阵营、AOE 展开和 LOS 结论；多目标 AOE 只执行一次图并扣除一次资源。

`SkillGraphAbilityConfig` 可选引用 `BattlePresentationGraph`，并以 `VisualAction` 选择 None、Melee、Ranged 或 Cast Tween 模板；可选 `PoseFamily` 表达毒矛等明确物理动作，未指定时由角色 `UnitActionPoseProfile` 选择默认族。玩法仍由 SkillGraph 决定目标、伤害、Buff 和分支；Presentation Graph 只编排强类型语义入口、角色 Tween、投射物、第三方 Prefab FX、程序化 Recipe、Delay、Marker 与显式 Fork/Join。`Action` 的 Release Marker 至多一次启动玩法图，并把 Pose Family 与 Release 准备回调传给共享 Tween；`Projectile` 的 Impact Marker 控制命中继续。缺少 Graph、入口、姿态图或 Profile 时回退兼容路径/idle，不让表现依赖改变玩法成功边界。

毒矛 Lv1-Lv3 显式引用 `ThrownAttack`；无论是否通过 Presentation Graph，Release 都先切换空手视觉、清除投掷姿态，再执行玩法图，并在成功、失败或取消后按权威长矛状态对账。召回与分身继续按 Cast 解析无矛施法图，免费拾取保持 `VisualAction.None`。

`ProjectileLaunchNodeRecord` 可选引用 `ProjectileVisualProfile`，并继续完整往返 TravelTime、Speed、DropOnHit、LOS 与 Profile 资产路径。Profile 可选择程序化 Sprite/SoftDisc 或 Flight Prefab，并可配置 Impact Prefab、命中寿命和缩放；运行时按 `worldDistance / Speed` 计算并限制飞行时长，终点在发射时锁定。无 Flight Prefab 时由共享 Factory 创建程序化投射物和 Particle/Ghost Trail；有 Flight Prefab 时从 `TransientVfxPool` 租用并复用同一弹道、轨迹和取消清理路径。火球使用程序化软圆热核与 World-space 尾火，骨矛使用独立中心 Sprite、切线旋转与最多两个短残影，毒矛使用 Piloto 飞行与命中 Prefab。Sprite Material 为空时保留 `SpriteRenderer` 默认兼容材质，显式 Material 继续传递给残影。抵达后先等待 Recipe 的 `ProjectileImpact` 接触关键帧，再写入 `ProjectileHit`；Prefab impact 有战斗 scope 时注册后允许玩法继续，无 scope 时由 caller 等待回收。取消先标记等待任务为取消，再 Kill Tween 并清理或回池所有临时对象，避免 `OnKill` 抢先完成或留下实例。

Skill VFX 使用有限原语 Recipe：六种强类型 Cue 只携带结算前捕获的世界坐标快照，六种固定原语由 `SkillVfxCoordinator` 统一创建、排序、等待和清理。只有层的 `BlockingMarker` 影响玩法继续时点；粒子与残影强制非阻塞。Cast 开始时以施法者 Sprite 中心发送 `CastCharge`，其 `BlockingMarker=0`；Recipe 按明确技能族、已有专属、默认 Cast 的顺序解析，在人物与阴影后生成单个径向光环，不修改主 Renderer。火球终点/溅射/Lv3 条件引爆、骨矛实际命中交叉闪光、突刺方向刺痕和实际命中反馈已分别接入；空 Recipe 或无 Sink 时保持 no-op。统一 Presentation Workbench 复用 Builder 时间采样，支持 Recipe/Cue/等级/路径/命中数与可拖动时间轴。

唯一入口 `Tactics/Pure Run/Presentation Workbench` 把 GraphView、隔离 Preview Stage、节点/Graph Inspector、Profile 沙盒和时间线统一到以 `BattlePresentationGraph` 为根的编辑会话。会话编辑隐藏克隆，Apply All 才以单一 Undo group 写回，Revert All 或关闭时 Discard 不污染正式资产；旧 Presentation Graph、Tween Preview 与 Skill VFX Preview 菜单已移除。交互 Preview 的 retained UI Toolkit `Image` 显示固定 `1280×720` 持久 RenderTexture，独立控制器在播放时最高 30 FPS；连续窗口或分栏 resize 会暂停 GPU 渲染，稳定 500ms 且至少三个 update 后只补最新一帧。中央预览不再经过 IMGUI 绘制或 `EndAndDrawPreview()`。18 个正式图仍以 Editor-only Phase/Cue 场景完整演示代表性成功命中，Scenario 不参与 Runtime 编排或玩法结算。

Runtime 与 Preview 共用纯数据 `PresentationExecutionPlan` 解释顺序、禁用节点、Finish 和嵌套 Fork/Join，叶执行器仍分别负责 Tween、Projectile、Prefab FX、Recipe、Delay 与 Marker。Agent 侧提供五个 Presentation MCP 工具；图快照除 Graph revision 外还返回所有可编辑叶资产的 GUID、路径、类型、revision 与引用者，节点资产引用携带同一 revision。typed ChangeSet 支持在单一 Undo/SaveAssets 事务中创建 Graph、创建/复制/修改叶资产并重绑，Recipe 以 `replaceRecipeBindings` 整表替换，不开放任意 SerializedProperty。preview 的 Full Scenario、Phase、Entry、Leaf 与 Fork Region scope 共用 Workbench 无窗口内核，返回对应 PNG、固定 seed、真实节点/Marker/Phase 时间线、诊断与实际 fallback。

当前火球、骨矛和突刺 Recipe 是已验收的可玩临时视觉基线，不是复杂技能的目标品质或永久回退。长期制作策略保留 Tween 处理角色姿态、位移、受击、后坐和投射物运动，也允许简单光环、短闪光、短尾迹与颜色脉冲继续程序化；多阶段、形态复杂或承担职业识别的技能特效后续逐个改用美术可直接调整的 Prefab、ParticleSystem、Shader/Material、Sprite 序列或 AnimationClip。替换完成前保留现有 Recipe，不继续将有限原语扩充为通用复杂 VFX 框架。
`PlayPresentationCue` 是 SkillGraph 到表现层的通用语义请求节点，并通过 Spec/MCP 往返 `PresentationCueKind`；旧 `PlayVisualCue` 保留兼容但不再是新样本的权威入口。霹雳闪电已迁移为 `Cast Tween → Release → SkillGraph → PrimaryTargetHit Prefab FX`；伤害加深诅咒 Lv1–Lv3 的目标命中入口以闭合三分支 Fork/Join 并行播放 `Ground Sigil V2`、远侧火焰与近侧火焰，随后立即发送 Impact；毒矛已迁移为 `Ranged Tween → Release → Projectile → Impact → 中毒/落矛`。第三方 Prefab 的 FireAndForget 仍交给 `BattleRuntimeScope` 跟踪，取消和非取消异常保持可观察。

`TransientVfxPool` 按 Prefab 共享临时粒子实例，每个 Prefab 最多缓存 8 个对象；重复 Return 不会把同一对象入池两次，超额实例直接销毁。`SubsystemRegistration` 会清空静态缓存，避免禁用 Domain Reload 时把上一轮 Play 状态带入下一轮。one-shot 取消仍会在 `finally` 回池并作为正常 teardown 被 scope 忽略，其他异常保留为 fault 交给 scope 或 await caller 观察。暖池 Rent/Return 的 PlayMode 回归要求 0 B managed allocation；暂停、2×/4× gameplay speed、目标在途销毁和 scope drain 前 impact 回池也由真实 coordinator 路径验证。

Unity 图编辑器支持创建、连线、属性编辑、搜索和校验。Agent 可通过 `SkillGraphSpec`、`SkillGraphSpecCompiler` 与 `SkillGraphSpecAutoFixer` 建立结构化输入，并使用 MCP 工具生成、校验和应用资产；运行语义继续由 Gameplay Test/PlayMode 测试证明。`SkillGraphAssetMenu` 生成的怪物技能模板与正式数值一致（Charge Strike 伤害 8、Area Blast 伤害 6），`CreateAsset` 可重入：重复生成不失败也不留下半生成状态，由 `SkillGraphAssetMenuEditorTests` 锁定。

`SkillTargetingProtocol` 在图资产上统一表达主目标、任意格中心、方向扇形、有序多段目标、实体对象格、回收动作和无路径移动；`OrderedTargetSelectionState` 维护分段选择、重复拒绝、取消上一段与完成条件。玩家输入、AI 与 Gameplay Test 可消费同一协议，不各自推导一套阶段规则。召唤 executor 也不覆盖 Battle System 的派生移动公式：Speed 4 的骷髅法师和火魔均为移动 2，先攻仍为 8。

目标选择阶段的视觉朝向由共享 `FacingCoordinator` 处理：合法和非法的格子/单位悬停都可更新施法者方向，移动目标优先使用路径第一段；取消、离开或失败释放保留最后预览。有序多目标在进入选择时锁定合法锥形方向，后续视觉转向不会改变该范围。

结构化入口将该协议保存在 `SkillGraphSpec.Targeting`；Spec 编译、克隆和导出完整往返全部 targeting 字段，保证 MCP/JSON 重建后语义不丢失。

`AbilityConfig.MaxUsesPerTurn` 为 SkillGraph 能力提供每回合成功使用上限：`0` 不限，正数按配置的稳定 `DisplayName` 在 Unit 上独立计数，并在 `PrepareForTurn` 重置；缺失稳定名称的限次能力 fail-closed。只有图以 `Completed` 结束才计次，失败或取消不计；AI 与 UI 复用同一 `CanPerform`/可用性结论，use policy、availability policy 与 basic ability 提交边界保持兼容，运行时次数不存入共享资产。`SkillAbilityUsesPerTurnTests` 覆盖稳定 key、回合重置、0/正数上限、Completed/失败边界及 policy/basic 兼容；相关运行时回归由 `SkillGraphRuntimeTests` 覆盖。

节点集合现包含 `ApplyMana`、`RemoveHarmfulBuffs`、兼容表现节点 `PlayVisualCue`、语义表现节点 `PlayPresentationCue`、法师等级语义节点 `MageSkill`、死灵法师等级语义节点 `NecromancerSkill` 与亚马逊等级语义节点 `AmazonSkill`，`SelectAlly` 可显式允许自身成为合法友军目标。伤害节点分别保存伤害大类和元素；`ApplyBuff.RequiresSuccessfulHit` 只在明确的命中附带状态上读取前一伤害节点结果，独立 Buff 不受历史命中结果污染。`SummonUnit` 可声明召唤物是否接受普通治疗，并通过 `SummonRegistry` 按召唤者、类别、上限和创建顺序管理最早替换；骷髅与骷髅法师关闭普通治疗，火魔保持开启。召唤执行先验证尸体、生成格和替换集合，再以事务顺序提交尸体、法力和旧召唤；选择尸体节点保留玩家实际点击目标而不再扫描并消耗所有尸体。运行时能力可注入使用策略与可用性策略：策略负责额外合法性、稳定禁用原因、动态显示名和成功完成后的资源提交；图失败时不会扣除资源，执行失败时恢复到点击前的最后预览朝向。Pure Run 消耗品使用该边界实现明确友军目标、每名角色每轮一次，并在图完成后提交对应独立实例。

法师等级链使用独立 AbilityConfig/SkillGraph：火球术 Lv1 单体、Lv2 十字溅射、Lv3 先引爆主目标旧点燃；寒冰箭 Lv3 增加一次稳定最近目标反弹；霹雳闪电为无 projectile/LOS 的瞬时直击；召唤火魔支持原子批量替换；冰甲 Lv2 对相邻近战攻击者附加 Slow；瞬移 Lv1 的目标预览遵循节点声明的 LOS，Lv2 显式取消 LOS 要求。固定 10×10 棋盘使用统一射程基准：火球、寒冰箭、闪电、诅咒、恐惧、骨矛和瞬移为 4，毒矛与召回长矛为 5；火魔召唤保持 3，尸体选择的 999 是明确全图例外。`PureRunRangeCalibrationAssetBuilder` 同时锁定 builder 常量、实际 AbilityConfig/SkillGraph 与怪物 Brain，防止重建资产回写旧值。资产目录校验约束“已发布等级连续且可加载”，法师已完成 1..MaxLevel 发布，其他职业将在对应切片完成。

死灵法师等级链同样使用独立 AbilityConfig/SkillGraph，并由 `NecromancerSkillNodeExecutor` 执行骷髅、骷髅法师、诅咒、恐惧、骨矛和骨盾的等级语义。Projectile 节点可显式关闭通用 LOS 并允许空格端点，骨矛再以自身规则解析墙体、首敌命中或直线穿透；Lv1–Lv3 的目标预览与执行都限制为正交或 45° 对角直线，Lv1/Lv2 只接受直线首敌，Lv3 才允许空格/单位端点并贯穿路径。等级资产由编辑器构建器生成，既有 Lv1 路径原位升级以保持 GUID。

亚马逊等级链由 `AmazonSkillNodeExecutor` 执行突刺、连续刺击、毒矛、回收/拾取长矛和诱饵。突刺端点代表方向而非必须有敌人的单位目标，通用 LOS 不会因为射线上先命中的敌人隐藏更远端点；执行器仍在友军、墙体或非法格处截断，并只对实际成功命中发送 VFX Cue。连续刺击消费 `OrderedTargetSelectionState` 的有序目标序列并逐段结算；毒矛在技能效果提交前预验证确定性落点，实体长矛由共享战斗状态注册，并以拥有者引用为唯一归属真相；缓存丢失时从活体实体重建，拥有者、落点、占格与卡片可用性不一致会输出诊断。通用 projectile LOS 与骨矛自定义直线解析均忽略落地长矛，但长矛仍保持占格。未持矛限制只作用于包含直接伤害节点的近战基础图及明确持矛技能，不再误伤移动图；移动预览通过独立 `CellGuidanceType` 图层显示长矛位置和可站立拾取位置，不改变合法目标集合。

技能事件记录允许同步致死在效果结算中立即销毁目标；目标已失效时保留事件类型和节点 ID，但不再访问其名称、格子或其他 Unity 对象属性。Projectile travel 属于游戏世界时长，统一通过可取消的 scaled delay 执行，因此暂停期间不会提前命中，并随所选 2×/4× 倍率加速；SkillGraph watchdog 继续使用 realtime，避免暂停使保护失效。

# Relationships

- [Battle System](battle.md)提供单位、格子、目标和效果环境。
- [Monster AI](monster-ai.md)通过共享合法性和执行接口选择技能。
- [Gameplay Test Framework](gameplay-test-framework.md)验证目标、阶段、状态与投射物结果。
- [Roguelike Run](roguelike-run.md)使用运行时 SkillGraph 模板执行战斗消耗品。
- 三职业首批技能的完成记录保留在[Archived Outcome](../plans/first-slice-three-class-skills.md)。
- 后续静态校验增强见[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

验证单个技能时同时检查 `SkillGraphAsset`、AbilityConfig/节点执行器和对应 PlayMode 或 gameplay spec。Unity 资产通过编辑器、MCP 或项目资产工具修改，不直接写 YAML。

# Citations

[1] [SkillGraph runtime](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Skills/Graph)
[2] [SkillGraph MCP tools](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs)
[3] [FirstSliceSkillAssetTests](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs)

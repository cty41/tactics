---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Skills/Graph
title: SkillGraph
description: 技能资产、解释器、Ability 桥接、共享目标规则和 Agent-first 创作验证主链。
tags: [gameplay, skills, skill-graph, unity]
timestamp: "2026-07-31T20:44:06+08:00"
status: active
catalog_scope: skill-graph
repo_paths:
  - .agents/docs/skill-graph-system.md
  - .agents/skills/skill-graph-creation/SKILL.md
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphAsset.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/ProjectileVisualProfile.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/ProjectileVisualCoordinator.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/ProjectileTweenBuilder.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRunner.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphSpec.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillTargetingProtocol.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/OrderedTargetSelectionState.cs
  - Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphSpecCompiler.cs
  - Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/AbilityConfig.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityImpl.cs
  - Assets/Tactics/Battle/Abilities/SkillGraphs
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
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:3b4574e67f461d2445d6614206fd48603547d4332b0b0dd67a6ef96899fe4edd
---

# Current State

`SkillGraphAsset` 保存编辑态节点图，`SkillGraphRunner` 解释执行，`SkillGraphAbilityImpl` 接入既有 `IAbility`、共享 targeting 和计划执行接口。玩家预览、AI 候选及执行前重验证复用射程、阵营、AOE 展开和 LOS 结论；多目标 AOE 只执行一次图并扣除一次资源。

`SkillGraphAbilityConfig` 以显式 `VisualAction` 选择 None、Melee、Ranged 或 Cast。视觉 release 标记至多一次启动图执行；恢复段与图后续节点并行，Ability 等待两者完成。缺少视觉组件或 Profile 时立即执行图，不让表现依赖改变玩法成功边界。

`ProjectileLaunchNodeRecord` 可选引用 `ProjectileVisualProfile`，并继续完整往返 TravelTime、Speed、DropOnHit、LOS 与 Profile 资产路径。运行时按 `worldDistance / Speed` 计算并限制飞行时长，缺图时只保留延迟；有图时创建临时 SpriteRenderer，终点在发射时锁定，到达后才进入 OnHit。取消会先标记等待任务为取消，再 Kill Tween 并清理 Renderer，避免 `OnKill` 抢先完成。毒矛已显式接入 `ProjectileLaunch → OnHit → AmazonSkill`，实体落矛仍由 Amazon 效果节点独立处理；物理基础、普通/毒矛及羊魔临时物理远程复用赤柴长矛，奥术/火焰/冰霜共用法师奥术弹并以 Tint 区分，Bone Spear 使用死灵飞行能量球。

Unity 图编辑器支持创建、连线、属性编辑、搜索和校验。Agent 可通过 `SkillGraphSpec`、`SkillGraphSpecCompiler` 与 `SkillGraphSpecAutoFixer` 建立结构化输入，并使用 MCP 工具生成、校验和应用资产；运行语义继续由 Gameplay Test/PlayMode 测试证明。

`SkillTargetingProtocol` 在图资产上统一表达主目标、任意格中心、方向扇形、有序多段目标、实体对象格、回收动作和无路径移动；`OrderedTargetSelectionState` 维护分段选择、重复拒绝、取消上一段与完成条件。玩家输入、AI 与 Gameplay Test 可消费同一协议，不各自推导一套阶段规则。

目标选择阶段的视觉朝向由共享 `FacingCoordinator` 处理：合法和非法的格子/单位悬停都可更新施法者方向，移动目标优先使用路径第一段；取消、离开或失败释放保留最后预览。有序多目标在进入选择时锁定合法锥形方向，后续视觉转向不会改变该范围。

结构化入口将该协议保存在 `SkillGraphSpec.Targeting`；Spec 编译、克隆和导出完整往返全部 targeting 字段，保证 MCP/JSON 重建后语义不丢失。

`AbilityConfig.MaxUsesPerTurn` 为 SkillGraph 能力提供每回合成功使用上限：`0` 不限，正数按配置的稳定 `DisplayName` 在 Unit 上独立计数，并在 `PrepareForTurn` 重置；缺失稳定名称的限次能力 fail-closed。只有图以 `Completed` 结束才计次，失败或取消不计；AI 与 UI 复用同一 `CanPerform`/可用性结论，use policy、availability policy 与 basic ability 提交边界保持兼容，运行时次数不存入共享资产。`SkillAbilityUsesPerTurnTests` 覆盖稳定 key、回合重置、0/正数上限、Completed/失败边界及 policy/basic 兼容；相关运行时回归由 `SkillGraphRuntimeTests` 覆盖。

节点集合现包含 `ApplyMana`、`RemoveHarmfulBuffs`、法师等级语义节点 `MageSkill`、死灵法师等级语义节点 `NecromancerSkill` 与亚马逊等级语义节点 `AmazonSkill`，`SelectAlly` 可显式允许自身成为合法友军目标。伤害节点分别保存伤害大类和元素；`ApplyBuff.RequiresSuccessfulHit` 只在明确的命中附带状态上读取前一伤害节点结果，独立 Buff 不受历史命中结果污染。`SummonUnit` 可声明召唤物是否接受普通治疗，并通过 `SummonRegistry` 按召唤者、类别、上限和创建顺序管理最早替换；骷髅与骷髅法师关闭普通治疗，火魔保持开启。召唤执行先验证尸体、生成格和替换集合，再以事务顺序提交尸体、法力和旧召唤；选择尸体节点保留玩家实际点击目标而不再扫描并消耗所有尸体。运行时能力可注入使用策略与可用性策略：策略负责额外合法性、稳定禁用原因、动态显示名和成功完成后的资源提交；图失败时不会扣除资源，执行失败时恢复到点击前的最后预览朝向。Pure Run 消耗品使用该边界实现明确友军目标、每名角色每轮一次，并在图完成后提交对应独立实例。

法师等级链使用独立 AbilityConfig/SkillGraph：火球术 Lv1 单体、Lv2 十字溅射、Lv3 先引爆主目标旧点燃；寒冰箭 Lv3 增加一次稳定最近目标反弹；霹雳闪电为无 projectile/LOS 的瞬时直击；召唤火魔支持原子批量替换；冰甲 Lv2 对相邻近战攻击者附加 Slow；瞬移 Lv2 取消可见性要求。资产目录校验约束“已发布等级连续且可加载”，法师已完成 1..MaxLevel 发布，其他职业将在对应切片完成。

死灵法师等级链同样使用独立 AbilityConfig/SkillGraph，并由 `NecromancerSkillNodeExecutor` 执行骷髅、骷髅法师、诅咒、恐惧、骨矛和骨盾的等级语义。Projectile 节点可显式关闭通用 LOS 并允许空格端点，骨矛再以自身规则解析墙体、首敌命中或直线穿透；Lv1–Lv3 的目标预览与执行都限制为正交或 45° 对角直线，Lv1/Lv2 只接受直线首敌，Lv3 才允许空格/单位端点并贯穿路径。等级资产由编辑器构建器生成，既有 Lv1 路径原位升级以保持 GUID。

亚马逊等级链由 `AmazonSkillNodeExecutor` 执行突刺、连续刺击、毒矛、回收/拾取长矛和诱饵。连续刺击消费 `OrderedTargetSelectionState` 的有序目标序列并逐段结算；毒矛在技能效果提交前预验证确定性落点，实体长矛由共享战斗状态注册，并以拥有者引用为唯一归属真相；缓存丢失时从活体实体重建，拥有者、落点、占格与卡片可用性不一致会输出诊断。通用 projectile LOS 与骨矛自定义直线解析均忽略落地长矛，但长矛仍保持占格。未持矛限制只作用于包含直接伤害节点的近战基础图及明确持矛技能，不再误伤移动图；移动预览通过独立 `CellGuidanceType` 图层显示长矛位置和可站立拾取位置，不改变合法目标集合。

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

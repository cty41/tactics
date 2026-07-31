# SkillGraph 系统

## 文档定位

SkillGraph 是战斗技能的统一行为表达。它把技能行为从单体 C# 类拆成可序列化节点图，并让玩家施法、AI、编辑器和自动化测试执行同一份图数据。

## 运行链路

1. `SkillGraphAsset` 保存节点、连接、入口与技能元数据。
2. `SkillGraphRunner` 按连接顺序执行节点并维护运行上下文。
3. Ability 桥接层负责消耗、选目标、启动图和返回执行结果。
4. 玩家控制、AI 与 Gameplay Test 只通过公共入口触发技能，不各自复制技能逻辑。

节点覆盖伤害、治疗、Buff、位移、召唤、投射物、分支和多阶段行为。目标选择与合法性检查由共享 targeting 规则处理，避免 UI、AI 和测试对同一技能给出不同结论。

`SkillGraphAbilityConfig.VisualAction` 显式声明 `None / Melee / Ranged / Cast`，不按技能名、射程或伤害类型在运行时猜测。执行时先播放蓄力，到共享视觉 Sequence 的 release 标记才启动 SkillGraph；恢复动画与图执行并行，Ability 等待二者结束。缺少 Tween 组件或 Profile 时 release 立即发生，玩法不能因表现缺失而丢失。

`ProjectileLaunch` 可选引用 `ProjectileVisualProfile`。飞行时长按世界距离除以 Speed 计算并限制在 `0.12–0.75s`；`Speed <= 0` 时回退到至少 `0.05s` 的旧 `TravelTime`。投射物在发射时锁定终点，到达后才写入 `ProjectileHit` 并继续 `OnHit`；缺少 Profile 时仍保留相同时序，但不创建 Renderer。投射物是无碰撞、无占格、无阴影的临时视觉对象，不通过 `Resources.Load` 获取资产。

Pure Run 当前使用三张正式中心锚点 Sprite：赤柴长矛供物理基础攻击、普通/毒矛及羊魔临时物理远程复用，法师奥术弹通过 Profile Tint 表达奥术、火焰和冰霜，死灵飞行能量球专供 Bone Spear。配置器只对明确能力清单写入 `VisualAction`；未知能力保持 `None` 并报告，禁止按名称或射程猜测动画类型。

`SkillTargetingProtocol` 统一表达主目标、任意格中心、方向扇形、有序多段目标、实体对象格、回收动作和无路径位移。伤害大类与元素分别配置；`ApplyBuff.RequiresSuccessfulHit` 只在明确的“命中附带状态”节点上启用，避免独立 Buff 误读旧伤害结果。`SummonUnit` 通过战斗级 `SummonRegistry` 按召唤者和类别维护顺序、上限与最早替换。

目标选择期间，`SkillGraphAbilityImpl` 通过共享战斗朝向协调器预览施法者方向：单位和格子悬停都可改变视觉朝向，移动目标优先使用可达路径第一段，非法或无路径目标直接使用鼠标格方向。取消、离开目标或失败释放保留最后预览；有序多目标的合法锥形仍使用进入选择时锁定的方向，不随视觉预览漂移。完整生命周期见[战斗单位朝向规则](battle-facing-rules.md)。

结构化创作时，目标协议写入 `SkillGraphSpec.Targeting`。`SkillGraphSpecCompiler` 的编译、克隆和导出必须完整往返协议字段；若只保存在运行时资产而没有进入 Spec，MCP/JSON 重建会丢失多段选择和格子目标语义。

## 每回合限次（cantrip）

`AbilityConfig.MaxUsesPerTurn` 配置每回合成功使用上限：`0` 表示不限次数，正数表示该能力每回合最多成功完成的次数。计数属于 Unit 的回合运行时状态，以配置自身稳定的 `DisplayName` 为 key，并在 `PrepareForTurn` 清空；限次能力缺失稳定名称时禁用，而不是退化为不限次。

只有 SkillGraph 返回 `Completed` 才计次，失败或取消不消耗次数。AI 候选与战斗 UI 都复用能力的 `CanPerform`/统一可用性结果，因此达到上限后自然停止提供该能力。该限制与 use policy、availability policy 和 basic ability 边界兼容；basic ability 仍遵循自身的一次使用语义，完成提交不能重复计数。AbilityConfig/SkillGraph 资产可以被多个单位共享，但不得在资产上保存运行时计数，各单位的次数必须彼此独立。

## 创作入口

### Unity 图编辑器

适合人工查看和微调资产。编辑器支持节点创建、连线、属性编辑、搜索、校验和保存。Unity 序列化资产不得直接编辑 YAML，应通过 Unity 编辑器、MCP 或项目资产工具修改。

### Agent-first Spec

`SkillGraphSpec` 是 Agent 可写的结构化输入；`SkillGraphSpecCompiler` 将其编译为图资产所需的数据，`SkillGraphSpecAutoFixer` 处理可安全自动修复的问题。MCP 入口提供生成、校验和应用能力。

推荐流程：

1. 从技能目录和设计约束建立 Spec。
2. 先运行结构与语义校验。
3. 只对确定性问题执行自动修复。
4. 通过 MCP/Unity 工具生成或更新资产。
5. 用 Gameplay Test 验证实际目标、阶段和结果。

## 校验边界

当前图校验覆盖唯一 Start、至少一个终止节点、边端点、自引用边、Start 入边、终止节点出边、孤立/不可达节点和简化数据依赖；Runner 另以 `MaxSteps` 中止可能的无限循环。完整环路、阶段和目标语义并未全部静态证明，仍需用运行测试验证伤害对象、范围、阶段顺序和状态结果。

## 代码与数据位置

- 运行时：`Assets/Tactics/Scripts/Common/Skills/Graph/`
- Ability 桥接：`Assets/Tactics/Scripts/Common/Units/abilities/`
- 编辑器：`Assets/Tactics/Scripts/Editor/SkillGraphEditor/`
- MCP 工具：`Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs`
- 技能资产：`Assets/Tactics/Battle/Abilities/SkillGraphs/`
- 技能目录与测试：以仓库内实际 catalog、EditMode/PlayMode 测试为准

尚未完成的增强项统一记录在 [项目已知缺口](project-known-gaps.md)。

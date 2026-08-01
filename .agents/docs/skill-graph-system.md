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

`ProjectileLaunch` 可选引用 `ProjectileVisualProfile`。飞行时长按世界距离除以 Speed 计算并限制在 `0.12–0.75s`；`Speed <= 0` 时回退到至少 `0.05s` 的旧 `TravelTime`。投射物在发射时锁定终点，到达后先发送 `ProjectileImpact` 语义 Cue，等待接触关键帧，再写入 `ProjectileHit` 并继续 `OnHit`；缺少 Profile、VFX Sink 或 Recipe 时仍保留玩法时序且立即完成表现调用。投射物是无碰撞、无占格、无阴影的临时视觉对象，不通过 `Resources.Load` 获取资产。

Pure Run 当前正式中心锚点 Sprite 包含赤柴长矛、法师奥术弹、死灵飞行能量球和独立骨矛；火球飞行改用程序化软圆热核，不再复用奥术梭形轮廓。骨矛使用 `128 PPU`、中心 Pivot、原生 `Scale=1`，沿飞行切线旋转并最多保留两个短残影，不再复用死灵能量球。Sprite Profile 未显式提供 Material 时，主投射物和残影必须保留 `SpriteRenderer` 的兼容默认材质；只有显式 Sprite Material 才允许覆盖，程序化 `SkillVfxPrimitive` 材质不得作为 Sprite 回退。配置器只对明确能力清单写入 `VisualAction`；未知能力保持 `None` 并报告，禁止按名称或射程猜测动画类型。

## 有限原语技能动效（临时视觉基线）

`SkillVfxRecipe` 将技能语义 Cue 映射到六种固定原语：`RadialCore`、`RadialRing`、`TaperedLine`、`CrossFlash`、`ParticleBurst` 和 `ProjectileGhostTrail`。它不是通用 VFX Graph；新增视觉先组合现有原语，只有无法表达且经过 Review 时才扩充词表。径向原语共用一个 URP Unlit Shader 和透明/加法两个 Material，实例参数由 `MaterialPropertyBlock` 提供；Quad、菱形和锥形线 Mesh 只在内存中创建并缓存。

SkillGraph 和三个职业执行器只发送 `CastCharge`、`ProjectileImpact`、`DirectionalStrike`、`PrimaryTargetHit`、`SecondaryTargetHit`、`ConditionalDetonation` 六种强类型 Cue，不直接创建 Renderer、Mesh 或 ParticleSystem。`SkillVfxCueContext` 是结算前捕获的不可变世界坐标快照，包含等级、起终点、方向、路径、实际命中点和强度；同步致死后不得回读可能已销毁的 Unit。`SkillVfxCoordinator` 统一负责层排序、关键帧等待、取消和清理。

`CastCharge` 在 Cast 纸片 Tween 开始时以施法者 Sprite 中心为锚点发送，`BlockingMarker=0`，不改变 `0.28s` release 与技能结算时序。Recipe 选择顺序为明确技能族、已有专属 Recipe、默认 Cast Recipe；默认光环为低饱和蓝，骨矛为苍白青，火球为暖橙红。光环的排序相对施法者为 `-2`，且不复制或修改主 `SpriteRenderer`。

每层只有一个 `BlockingMarker`。释放、飞行抵达和接触关键帧可以阻塞玩法；淡出、火星、骨屑和残影继续异步且不得拖延结算。`ParticleBurst` 与 `ProjectileGhostTrail` 强制非阻塞。取消技能或退出战斗必须清理临时 Renderer、ParticleSystem、Tween 和未完成任务；空 Recipe、缺材质或没有 Unity Renderer 的测试世界仍须正常完成技能。

当前首批配方：

- 火球：飞行软圆热核直径约 `0.17` 世界单位并带最多三粒 World-space 尾火；终点爆环在 `0.10s` 接触关键帧后结算，实际溅射目标使用弱环，Lv3 只有确实移除旧 Burning 时才发收缩引爆环。
- 骨矛：已确认的独立骨矛 Sprite 沿飞行切线旋转，残影每 `0.055s` 采样、寿命 `0.12s`、最多两个且非阻塞；实际命中点使用 `±35°` 交叉骨白闪光，`0.05s` 达峰，每点两粒骨屑且单次最多八粒。
- 突刺：先按 2/3 格规则扫描并保存最终路径，再让锥形刺痕于 `0.065s` 延伸到接触点后结算；通用敌人 LOS 不裁掉后方方向端点，友军、墙体和非法格仍终止扫描。实际命中复用无骨屑的琥珀交叉闪光。

上述火球、骨矛和突刺 Recipe 已达到可用标准，但只作为后续美术特效替换前的临时视觉基线，不代表复杂技能的目标品质或长期制作架构。后续不应默认扩充有限原语词表来承担复杂技能，也不预先承诺它们作为传统特效缺失时的永久回退。

### 特效制作边界

- 角色 Idle、移动、受击、攻击后坐、施法姿态和投射物位移继续由 Tween 负责，这些效果只改变视觉 Transform 或时序，不承担技能的核心美术身份。
- 低层数、短时长、简单几何的光环、短闪光、短尾迹和颜色脉冲可继续由程序化原语实现。
- 多阶段爆炸、明显形态变化、职业标志性技能或需要精细分层的效果，后续使用美术可直接调整的 Prefab、ParticleSystem、Shader/Material、Sprite 序列或 AnimationClip 制作。
- 传统特效按技能逐个接入并替换对应临时 Recipe；未实施替换前，当前程序化效果继续正常使用。

编辑器入口 `Tactics/Pure Run/Skill VFX Preview` 支持选择 Recipe、Cue、等级、路径长度和命中点数量，并提供播放、暂停、重播与时间拖动。窗口直接调用运行时 Builder 的时间采样函数；粒子使用固定种子的隐藏 ParticleSystem 并通过绝对时间 `Simulate` 重放。窗口同时显示 `64×32` Tile 参考线与阻塞关键帧，便于在不改变资产的情况下检查最高亮阶段。

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

# 怪物战斗 AI 系统统一计划

## Summary

- 本文件是怪物战斗 AI 的唯一权威计划入口，替代此前分散的 v1、review 后修正、完整修正、增强版、MCP 资产生成、AbilityUse 多技能候选化等计划。
- 当前架构采用“规则硬门禁 + 意图/动作候选打分 + 固定执行器 + 受约束 Graph Editor”的路线，不做通用行为树或任意可视化脚本系统。
- 当前已落地的核心资产与代码方向：
  - `AiBrainAsset`：怪物 AI 脑资产，引用决策图与评分风格。
  - `AiDecisionGraph`：保存 `Intent / Rule / Score` 节点与边。
  - `AIProfile`：保存评分维度开关、权重、曲线和随机扰动。
  - AI Graph Editor：支持节点创建、Inspector wrapper、自动布局、节点颜色和状态展示。
  - MCP workflow：允许 LLM agent 根据结构化怪物需求生成 graph/profile/brain 资产。

## Current State

- 运行时主流程已经形成：
  - `AiContextBuilder` 构建战斗快照。
  - `IntentGenerator` 枚举具体 `IntentCandidate`。
  - `RuleFilter` 执行硬门禁。
  - `IntentScorer` 聚合 graph/profile 评分。
  - `IntentResolver` 选择最佳候选。
  - `IntentExecutor` 复用现有移动、攻击、技能执行链。
- `AbilityUse` 已从“取第一个技能”升级为多技能候选化：
  - 候选粒度为“可达起点 + 技能 + 合法目标点 + 实际受影响目标组”。
  - 支持伤害、治疗、Buff、Debuff、控制、AOE 等 AI 标签。
  - 新增技能类规则和评分：`HasDamageAbility`、`HasHealAbility`、`HasAOEAbility`、`MultiTargetOpportunity`、`AbilityEffectiveness`、`AOEValue`、`HealUrgency`、`ControlValue`、`BuffUtility`、`DebuffUtility`。
- Mew 风格的职责边界已经落地到运行时契约：
  - `IAbilityTargetingProvider` 是玩家预览、AI 候选和执行前重验证的共同合法性入口；技能拥有射程、形状、阵营、空格目标、AOE 展开和 LOS 规则。
  - `AiActionPlan` 保存站位、技能、目标点和目标集合；`IPlannedAbilityExecutor` 按目标点只执行一次技能图，移动失败不会继续施法。
  - `AbilityRangeFit` 和 `FollowUpValue` 让 AI 在合法动作之间评价射程利用与后续集火；新资产可选择 `GraphWeight × ProfileWeight`，旧资产保持图权重兼容模式。
  - 平分使用稳定候选顺序，不再消费 Unity 全局随机流。
- 固定 Pattern 使用每单位运行时游标，不把状态写在共享 `AiBrainAsset` 或决策图上。Pattern 步骤没有合法动作或执行失败时回退 Generic AI，只有指定技能成功才推进。
- 首版明确不建立运行时 `threatValue` 或动态遭遇预算器；敌人压力由遭遇配方、布局和普通/精英/Special 倍率表达。
- `BasicMeleeGraph.asset` 已作为基础近战小怪模板重建：
  - Intent：`FinishOff`、`BasicAttack`、`Engage`、`Retreat`、`HoldPosition`。
  - Rule：攻击范围、移动攻击范围、可击杀、低血量、安全目的地。
  - Score：击杀可能、距离、目标血量。
  - 图布局为 Intent/Rule/Score 三列，无孤立 Rule/Score 节点。
- AI Graph Editor 当前能力：
  - 支持 Intent/Rule/Score 独立节点。
  - 支持节点位置、边关系、参数保存。
  - 支持 wrapper + Unity Inspector 编辑。
  - 支持自动布局和不同节点类型颜色展示。
- MCP 自动生成方向已经明确：
  - 不做写死模板的 Unity 菜单工具。
  - 由 LLM agent 使用 Unity MCP，根据结构化怪物需求直接创建和配置 `AiDecisionGraph`、`AIProfile`、`AiBrainAsset`。
  - `.agents/skills/monster-ai-mcp-workflow/SKILL.md` 是当前 MCP 操作规范入口。

## Architecture

### Runtime Flow

```text
AIPlayer / Unit
  -> AiBrainRunner
    -> AiContextBuilder
    -> IntentGenerator
    -> RuleFilter
    -> IntentScorer
    -> IntentResolver
    -> IntentExecutor
```

- `AiContext` 是一次决策快照，包含自身、敌我单位、候选目标、可达格和可用技能；其中局部位置安全评分不等同于遭遇 `threatValue`。
- `IntentCandidate` 是评分和执行的统一对象，不只是抽象意图名；它应携带目标、站位、技能、预估伤害/治疗/控制收益、规则失败原因和分项评分。
- `RuleFilter` 只做硬门禁，不做加减分。
- `IntentScorer` 负责把 score 节点或 profile 配置转换为分项分数。
- `IntentExecutor` 只负责把候选翻译到现有战斗执行链，不维护第二套战斗命令。

### Asset Model

- `AiBrainAsset`
  - 引用 `AiDecisionGraph`。
  - 引用 `AIProfile`。
  - 保存低血量阈值、击杀阈值、撤退基础分、调试开关等脑资产默认参数。
- `AiDecisionGraph`
  - 保存 `GraphNodeRecord` 多态节点列表。
  - 保存 `GraphEdgeRecord` 边列表。
  - 运行时展开为兼容旧接口的 `IntentNodeConfig` 列表。
- `AIProfile`
  - 保存评分维度是否启用。
  - 保存权重、`AnimationCurve`、随机扰动和风格标签。
  - 用于让同一张 graph 支持激进、防守、辅助等不同风格。

### Graph Editor

- Graph 结构固定为浅层：`Intent -> Rule/Score`。
- 不引入 Rule->Rule、Score->Score、Rule->Score、Score->Intent 等复杂控制流。
- 编辑器承担三件事：
  - 可视化结构和布局。
  - 通过 wrapper 暴露节点参数到 Unity Inspector。
  - 提供校验和自动布局，降低错误配置概率。

## Standard Templates

### 基础近战小怪

- 目标行为：
  - 有补刀机会优先 `FinishOff`。
  - 目标在攻击范围内时 `BasicAttack`。
  - 不在攻击范围但可接敌时 `Engage`。
  - 低血量且有安全格时 `Retreat`。
  - 无有效动作时 `HoldPosition`。
- 推荐 graph：
  - `FinishOff -> TargetInMoveAttackRange + TargetKillable + KillPotential + DistanceToTarget`
  - `BasicAttack -> TargetInRange + DistanceToTarget + TargetHealth`
  - `Engage -> TargetInMoveAttackRange + DistanceToTarget`
  - `Retreat -> HealthBelowThreshold(0.3) + DestinationSafe`
  - `HoldPosition -> DistanceToTarget`

### 多技能怪

- `AbilityUse` 仍是一个抽象 Intent，不为每个技能创建独立 Intent。
- 具体用哪个技能由运行时展开为多个“技能 + 目标/目标组 + 站位”候选后统一评分。
- Graph 可通过技能类 Rule/Score 表达偏好：
  - 伤害怪：`HasDamageAbility` + `AbilityEffectiveness` + `KillPotential`
  - AOE 怪：`HasAOEAbility` + `MultiTargetOpportunity(2)` + `AOEValue`
  - 控制怪：`HasControlAbility` + `ControlValue`
  - 治疗/辅助怪：`HasHealAbility` + `TargetNeedsHealing` + `HealUrgency`

## MCP Asset Generation Workflow

1. 将自然语言怪物需求归一化为结构化 spec：
   - `monster_name`
   - `style_label`
   - `intent_nodes`
   - `rule_nodes`
   - `score_nodes`
   - `edges`
   - `profile`
   - `brain_defaults`
   - `output_dir`
2. 使用 Unity MCP 创建或修改资产：
   - 创建/配置 `AiDecisionGraph`。
   - 创建/配置 `AIProfile`。
   - 创建/配置 `AiBrainAsset` 并绑定 graph/profile。
3. 所有 graph 节点必须写入 `_position`：
   - Intent：X=60。
   - Rule：X=340。
   - Score：X=620。
   - 待诊断孤立节点：X=900+，最终模板不应保留。
4. 静态校验：
   - 节点列表非空。
   - 边引用节点存在。
   - Intent 类型无重复。
   - 非诊断模板中没有孤立 Rule/Score 节点。
   - Brain 正确引用 graph 和 profile。

## Remaining Work

- 明确 `ScoreNode.Parameter` 的正式语义。目前它是序列化字段，但运行时尚未读取；推荐用于距离归一化上限、AOE 满分目标数、安全度缩放等 score-specific 参数。
- 为四类首版怪物创建并验证真实 SkillGraph、profile、graph 和 brain 资产；运行时接口已具备，资产必须通过 Unity MCP 工作流生成。
- 增加 AI 调试可视化：
  - 单回合决策日志面板。
  - 候选动作分项评分表。
  - 可选格子热力图。
- 扩充模板库：
  - 远程怪。
  - AOE 法师。
  - 治疗辅助怪。
  - 控制怪。
  - 高威胁精英怪。
- 增加固定 Pattern 的 Inspector/Graph 校验，防止空技能名和无法由单位技能集满足的步骤。
- 增加资产校验工具：
  - orphan 节点检测。
  - disabled 节点提示。
  - 重复 Intent 检测。
  - 缺少 fallback intent 检测。

## Deprecated Source Plans

以下旧计划已被本文件吸收，不再作为后续实现依据：

- `怪物战斗AI系统v1计划.md`
- `怪物战斗AI系统v1-review后修正计划.md`
- `怪物战斗AI系统v1-完整修正方案.md`
- `怪物战斗AI系统v1-完整修正方案-增强版计划.md`
- `基于MCP的怪物AI资产自动生成工作流计划.md`
- `AbilityUse多技能候选化开发计划.md`

如后续发现旧计划中仍有遗漏，应把仍有效的内容迁移进本文件，而不是恢复多个并行计划。

## Validation Checklist

- [ ] 新 AI 与旧行为树单位可共存。
- [ ] `BasicMeleeGraph.asset` 可加载、可校验、可视化布局清晰。
- [ ] `AbilityUse` 可为多个技能生成候选，而不是只选第一个技能。
- [ ] 玩家预览、AI 与执行前重验证使用同一 `IAbilityTargetingProvider` 结果。
- [ ] AOE 目标组只执行一次技能图并只扣除一次资源。
- [ ] 两只共享 Brain 的单位拥有独立 Pattern 游标，失败与 fallback 不推进。
- [ ] Graph Editor 可保存节点、边、位置和参数。
- [ ] MCP workflow 能根据结构化 spec 生成 graph/profile/brain。
- [ ] 修改 `.cs` 后必须执行 Unity 编译；仅修改本计划文档不需要编译。

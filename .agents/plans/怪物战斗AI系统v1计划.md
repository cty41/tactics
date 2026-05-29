# 怪物战斗 AI 系统 v1 计划

## Summary

- 目标：为战棋战斗新增一套与现有 `BehaviourTreeResource` 并行的怪物 AI 系统，核心模型采用“规则硬门禁 + 意图打分 + 固定执行器”，并提供受约束的可视化蓝图编辑器。
- 成功标准：程序可为怪物原型配置 `AiBrainAsset`；AI 能基于规则过滤和评分选择战斗意图；策划可在图编辑器中拼装和调参；旧行为树怪物保持不受影响。
- 当前结论：v1 只做决策层蓝图，不开放动作层自由编排；规则系统仅做硬门禁；评分对象是具体可执行方案；新系统与旧行为树并行存在。

## Current State

- 当前运行时 AI 入口是 [Assets/Tactics/Scripts/Common/players/AIPlayer.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Common/players/AIPlayer.cs)，按单位顺序执行 `Unit.BehaviourTree.Execute`。
- 单位当前通过 [Assets/Tactics/Scripts/Common/Units/Unit.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Common/Units/Unit.cs) 持有 `BehaviourTreeResource`，并在初始化时调用 `_behaviourTreeResource.Initialize(this, gridController)`。
- 现有默认怪物 AI 主要来自 [Assets/Tactics/Scripts/Common/AI/behaviourTrees/RegularBehaviourTreeResource.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Common/AI/behaviourTrees/RegularBehaviourTreeResource.cs)，本质是位置评估器 + 攻击序列的行为树。
- 项目已有可复用的 GraphView 风格编辑器，代表实现见：
  - [Assets/Tactics/Scripts/Editor/RoguelikeEventEditor/RoguelikeEventEditorWindow.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Editor/RoguelikeEventEditor/RoguelikeEventEditorWindow.cs)
  - [Assets/Tactics/Scripts/Editor/RoguelikeEventEditor/EventGraphView.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Editor/RoguelikeEventEditor/EventGraphView.cs)
  - [Assets/Tactics/Scripts/Editor/RoguelikeMapEditor/MapGraphView.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Editor/RoguelikeMapEditor/MapGraphView.cs)
- 项目规则约束：
  - 禁止 `Debug.Log`，统一使用 `TLog` / `TBattleLog`
  - 修改 `.cs` 后必须触发 Unity 编译
  - 新 C# 类型和 using 需要先验证项目实际命名空间和程序集边界

## Relevant Context

- 关键运行时入口
  - `AIPlayer.Play(GridController)`：单位回合循环和总入口
  - `Unit.Initialize(IGridController)`：单位 AI 初始化挂点
  - `BehaviourTreeResource.Initialize(IUnit, IGridController)`：旧 AI 初始化接口
- 现有 AI 能力基础
  - `Tactics.Common.AI.Evaluators` 已有位置和目标评估器，可复用其设计思路
  - `MoveActionNode`、`AttackSequenceNode`、`AttackActionNode` 等已有执行链路可作为新执行器的底层参考
- 编辑器基础
  - 项目已有 `GraphView + Inspector + Blackboard/侧栏` 组合范式
  - 已有窗口具备平移、缩放、拖线、右键创建、节点选中、预览/黑板布局
- 文档关联
  - [.agents/plans/战斗系统演进计划.md](/D:/codes/tactics/.agents/plans/战斗系统演进计划.md) 已明确 AI 适配是后续需要补齐的战斗演进项

## Implementation Changes

### Phase 1: 运行时新入口与兼容接入

- 新增 `AiBrainAsset` 作为怪物原型级 ScriptableObject，持有 `AiDecisionGraph`、默认参数、调试选项、版本信息。
- 新增 `AiBrainRunner` 作为单位单回合 AI 入口，职责固定为：
  - 构建 `AiContext`
  - 生成候选意图
  - 规则过滤
  - 评分聚合
  - 选择最佳候选
  - 调用执行器落地
- 修改 `AIPlayer`，在单位层增加“新 AI / 旧行为树”分流：
  - 单位若绑定 `AiBrainAsset`，走 `AiBrainRunner`
  - 否则继续执行 `BehaviourTree.Execute`
- 修改单位配置入口，使 `AiBrainAsset` 与 `BehaviourTreeResource` 互斥，避免双跑。
- 验收标准：
  - 新旧 AI 怪物可在同一战场共存
  - 未迁移单位行为保持不变
  - 新 AI 单位能通过统一入口完成单回合动作

### Phase 2: 决策流水线与数据模型

- 新增 `AiContextBuilder`，集中构建一次决策快照，至少包含：
  - 自身状态
  - 敌我单位列表
  - 可达格
  - 候选目标
  - 技能可用性
  - 危险区/安全度
  - 可击杀机会
- 新增 `IntentCandidate`，必须同时保存：
  - `IntentType`
  - 候选目标
  - 候选站位
  - 候选技能
  - 执行草案
  - 总分
  - 分项评分
  - 规则失败原因
- 拆分运行时职责为：
  - `IntentGenerator`
  - `RuleFilter`
  - `IntentScorer`
  - `IntentResolver`
  - `IntentExecutor`
- v1 固定支持 6 类原生意图：
  - 接敌
  - 普攻
  - 技能释放
  - 撤退保命
  - 追击残血
  - 待机/占位
- 明确约束：
  - 规则节点只做硬门禁，不做加减分
  - 评分节点只产出归一化分值和权重，不直接执行
  - 执行器负责把意图翻译成现有移动/攻击/技能命令
- 验收标准：
  - 候选意图的选择结果由规则 + 分数决定，而不是节点声明顺序
  - 执行失败时存在明确兜底，不会卡死该单位回合

### Phase 3: 蓝图资产与图模型

- 新增 `AiDecisionGraph` 资产及节点配置类型：
  - `IntentNodeConfig`
  - `RuleNodeConfig`
  - `ScoreNodeConfig`
  - `ModifierNodeConfig`
- 图模型限制为浅层受约束结构，仅允许：
  - `Root -> IntentNode`
  - `IntentNode -> RuleNode`
  - `IntentNode -> ScoreNode`
  - 少量白名单 `ModifierNode`
- 明确禁止：
  - 循环
  - 任意跨意图共享子图
  - `Rule -> Rule`
  - `Score -> Score`
  - 动作层自由编排
- 蓝图主要挂在怪物原型/职业层；单位实例只允许少量参数覆写，不按单只怪维护完整脑图。
- 验收标准：
  - 可用一份蓝图驱动多个同类怪物
  - 参数覆写不破坏共享图结构
  - 图校验能阻止不合法连线和结构

### Phase 4: 编辑器与调试工具

- 基于现有 `GraphView` 路线实现 AI 图编辑器，不新造完全不同的编辑器框架。
- 编辑器最少支持：
  - 创建节点
  - 拖线连接
  - Inspector 参数编辑
  - 保存 / 加载
  - 图合法性校验
  - 关键参数摘要显示
- 调试深度定为“日志 + 节点高亮”：
  - 显示候选意图列表
  - 显示规则淘汰原因
  - 显示评分分项
  - 显示最终选中意图
  - 在图上高亮本回合命中节点
- v1 不做：
  - 单步回放
  - 子图/宏节点
  - 通用可视化脚本系统
- 验收标准：
  - 重新打开编辑器后图资产可正确恢复
  - 调试模式下能从图和日志中解释“为什么 AI 这么选”

## Interfaces / Data Flow

### 主要公共类型

- `AiBrainAsset`
  - 持有静态 AI 配置和图资产
- `AiDecisionGraph`
  - 描述受约束的意图、规则、评分节点关系
- `AiContext`
  - 单回合只读决策快照
- `IntentCandidate`
  - 具体可执行方案的中间结果
- `AiDecisionLog`
  - 调试和可解释性输出载体

### 单回合调用链

1. `AIPlayer` 选中当前可行动单位
2. 判断单位绑定的是旧行为树还是新 AI 资产
3. 若走新 AI：
   - `AiContextBuilder` 构建 `AiContext`
   - `IntentGenerator` 根据图枚举候选
   - `RuleFilter` 剔除非法候选并记录原因
   - `IntentScorer` 对剩余候选生成分项评分与总分
   - `IntentResolver` 处理排序、平局和最低阈值
   - `IntentExecutor` 落地为实际移动/攻击/技能命令
   - `AiDecisionLog` 输出可解释信息
4. 若走旧行为树：继续 `BehaviourTree.Execute`

### 关键接口约束

- `IntentCandidate` 必须是“可执行方案”，不能只保存抽象意图名。
- `IntentExecutor` 不允许把动作级时序暴露回蓝图层。
- `RuleNodeConfig` 和 `ScoreNodeConfig` 必须职责分离，避免同一节点既裁剪又打分。
- `AiBrainAsset` 与 `BehaviourTreeResource` 默认互斥，不支持单位同回合双系统混合求值。

## Test Plan

### 自动检查

- 新增运行时类型和编辑器类型后，脚本零编译错误。
- 关键数据模型具备序列化和反序列化稳定性。
- 图结构校验能拦截非法连线和循环。
- 候选排序、平局决策、最低阈值和兜底逻辑具备单元测试或等效测试覆盖。

### 手工验证

- 在测试怪物原型上配置 `AiBrainAsset`，确认能正常走新 AI 入口。
- 同一战斗中同时放入旧行为树怪物和新 AI 怪物，确认双方都能正常行动。
- 低血量场景下验证撤退优先级。
- 存在残血可击杀目标时验证追击行为。
- 无技能目标时验证技能意图被规则淘汰，并在日志中看到原因。
- 编辑器中修改权重后重新运行战斗，确认行为变化可观察、可解释。

### 回归场景

- 旧 `RegularBehaviourTreeResource` 怪物行为不回退。
- 单位初始化、回合结束、战斗结算流程不因新 AI 接入而被破坏。
- 编辑器打开、保存、关闭后不会破坏已有 GraphView 相关工具。

## Risks / Open Questions

- 现有单位配置入口需要决定最终落点，是扩展 `Unit` 直接持有新 AI 资产，还是通过中间配置对象挂接；实现前需要结合现有序列化和 prefab 习惯定最终字段位置。
- 评分依赖的“危险区”“可击杀机会”等上下文若计算过重，可能影响多单位回合性能；实现时要避免每个节点重复扫描战场。
- `IntentExecutor` 若过度耦合现有行为树节点实现，后续扩展会受限；应优先依赖更底层的移动/技能/目标选择能力。
- 编辑器若直接复用现有事件图代码，需要注意命名、样式和数据模型不要与 Roguelike 编辑器耦死。

## Assumptions

- 默认采用决策层蓝图，不开放动作层自由编排。
- 规则系统只做硬门禁，不承担分数修正职责。
- v1 只做单次主决策，不做单位单回合内多段重规划。
- v1 不做团队协同意图，集火、保护、卡位、诱敌放到后续迭代。
- v1 不废弃现有行为树体系，优先保证低风险并行接入。
- 蓝图作者模型默认是“程序搭框架 + 策划调参”。

## Handoff Notes

- 新 session 开始执行前，先读：
  - [Assets/Tactics/Scripts/Common/players/AIPlayer.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Common/players/AIPlayer.cs)
  - [Assets/Tactics/Scripts/Common/Units/Unit.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Common/Units/Unit.cs)
  - [Assets/Tactics/Scripts/Common/AI/behaviourTrees/BehaviourTreeResource.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Common/AI/behaviourTrees/BehaviourTreeResource.cs)
  - [Assets/Tactics/Scripts/Common/AI/behaviourTrees/RegularBehaviourTreeResource.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Common/AI/behaviourTrees/RegularBehaviourTreeResource.cs)
  - [Assets/Tactics/Scripts/Editor/RoguelikeEventEditor/RoguelikeEventEditorWindow.cs](/D:/codes/tactics/Assets/Tactics/Scripts/Editor/RoguelikeEventEditor/RoguelikeEventEditorWindow.cs)
- 实施时先做运行时入口和数据模型，再做编辑器；不要先做 GraphView 外壳。
- 不要把 v1 扩成通用行为树/可视化脚本系统；不要加入团队协同、逐步回放、子图宏节点。
- 编写 C# 前先按项目规则验证目标类型、命名空间和程序集边界；写完后必须编译验证。

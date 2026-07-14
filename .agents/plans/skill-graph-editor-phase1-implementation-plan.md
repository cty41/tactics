# SkillGraph 技能图编辑器阶段1实现计划

## 关联文档

- 设计文档：[`../docs/skill-graph-editor-design.md`](../docs/skill-graph-editor-design.md)

## Background

阶段1目标是做出 **SkillGraph 最小闭环**，优先验证：

1. 文本/结构化描述可以落为 `SkillGraphAsset`
2. Unity 内可以查看和修正这张图
3. 运行时可以直接解释执行这张图
4. 先跑通两类代表性流程技能：
   - 冲向目标并造成伤害，命中后击退
   - 选点范围伤害

本阶段**不做**：

- 成长/升级/存档层统一
- 动画/VFX 预览
- 完整 DSL
- 全量替换旧 `AbilityConfig`

## Scope

本阶段交付物：

1. `SkillGraphAsset` 与最小节点/边模型
2. `SkillGraph` 基础校验器
3. `SkillGraph` EditorWindow + GraphView 最小可编辑版本
4. `SkillGraphRuntimeDefinition`、`SkillExecutionContext`、`SkillGraphRunner`
5. 一套最小节点执行器，足够支撑两个样例技能
6. 一个兼容旧战斗系统的桥接入口，让现有单位能执行 `SkillGraph`
7. 面向 MCP/Agent 的最小资产操作入口：创建、读取、校验

## Files

### Create

- `Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphAsset.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphValidation.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRuntimeDefinition.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/SkillExecutionContext.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRunner.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/ISkillNodeExecutor.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/SkillNodeExecutionResult.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/SkillNodeExecutorRegistry.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/StartNodeExecutor.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/SelectPrimaryTargetNodeExecutor.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/DashToTargetNodeExecutor.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/ApplyDamageNodeExecutor.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/ApplyKnockbackNodeExecutor.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/SelectTargetPointNodeExecutor.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/CollectTargetsInAreaNodeExecutor.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/ForEachTargetNodeExecutor.cs`
- `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/FinishNodeExecutor.cs`
- `Assets/Tactics/Scripts/Common/Units/Abilities/SkillGraphAbilityConfig.cs`
- `Assets/Tactics/Scripts/Common/Units/Abilities/SkillGraphAbilityImpl.cs`
- `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphEditorWindow.cs`
- `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphView.cs`
- `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphNodeViewFactory.cs`
- `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphNodeWrapper.cs`
- `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphAssetMenu.cs`
- `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphMcpFacade.cs`
- `Assets/Tactics/Arts/UI/SkillGraphStyle.uss`

### Modify

- `Assets/Tactics/Scripts/Common/Units/Abilities/AbilityConfig.cs`
- `Assets/Tactics/Scripts/Common/Units/Abilities/GenericAbilityImpl.cs`
- `Assets/Tactics/Scripts/Common/Units/abilities/ChargeAttackEffect.cs`（仅在复用边界不够清晰时调整；优先不改）
- `Assets/Tactics/Scripts/Editor/MonsterAIEditor/AiDecisionGraphView.cs`（仅在抽公共 GraphView 基类值得时改；否则不动）

### Asset Samples / Docs

- `Assets/Tactics/Battle/Abilities/SkillGraphs/ChargeStrike_Lv1.asset`
- `Assets/Tactics/Battle/Abilities/SkillGraphs/AreaBlast_Lv1.asset`
- `.agents/docs/skill-graph-mcp-contract.md`

## 设计约束

1. **不改成长层数据结构**：`SkillDatabase` / `SkillSystem` / `LearnedSkills` 不进入阶段1实施。
2. **不做完整自由图**：仅支持设计稿中定义的最小节点子集。
3. **旧系统继续可用**：`AbilityConfig` 旧路径不回归；新图能力通过桥接配置单独接入。
4. **优先复用现有结算**：伤害、治疗、Buff、击退仍尽量调用既有实现，不重写整套战斗规则。
5. **先保证 Agent/MCP 闭环成功率**：比手工编辑 UX 更优先。

## Tasks

### Task 1: 建立 SkillGraph 资产与校验骨架

**目标**：定义编辑态图模型、节点/边记录、基础节点类型与三层校验入口。

**Files:**
- Create: `Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphAsset.cs`
- Create: `Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphValidation.cs`

- [ ] 定义 `SkillGraphAsset`、节点基类、边记录、节点类型枚举
- [ ] 为阶段1节点建立最小参数模型：`Start`、`SelectPrimaryTarget`、`DashToTarget`、`ApplyDamage`、`ApplyKnockback`、`SelectTargetPoint`、`CollectTargetsInArea`、`ForEachTarget`、`Finish`
- [ ] 实现基础操作：新增节点、删节点、连边、查节点、清空
- [ ] 实现 `Validate(out errors, out warnings)` 入口
- [ ] 实现第一批校验：单入口、终止节点、非法边、孤立节点、缺少必填参数、超出首版支持域

**验收标准**
- 能在纯数据层创建一张合法图
- 能明确报出无入口、无终止、非法连接、缺参数等错误

### Task 2: 建立 SkillGraph 最小编辑器

**目标**：提供与 AI Graph 同风格的最小可视编辑器，用于查看、修改、校验图资产。

**Files:**
- Create: `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphEditorWindow.cs`
- Create: `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphView.cs`
- Create: `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphNodeViewFactory.cs`
- Create: `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphNodeWrapper.cs`
- Create: `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphAssetMenu.cs`
- Create: `Assets/Tactics/Arts/UI/SkillGraphStyle.uss`

- [ ] 复用 AI Graph 编辑器结构，建立 SkillGraph EditorWindow 工具栏：Load / Save / Validate / New / Clear
- [ ] 建立 GraphView 的节点恢复、边恢复、右键菜单添加节点、删除节点/边、保存节点位置
- [ ] 建立节点选中后 Inspector 编辑参数的 wrapper 流程
- [ ] 为阶段1节点提供最小 NodeView 表现与端口约束
- [ ] 接入 `SkillGraphValidation`，在编辑器中显示错误/警告

**验收标准**
- 能创建、保存、重新打开 SkillGraph 资产
- 能在编辑器中手工搭出两个样例技能图
- 非法连接与校验失败能直接看到反馈

### Task 3: 建立运行时定义与执行上下文

**目标**：把编辑态图转换成轻量运行时视图，并定义单次施法上下文。

**Files:**
- Create: `Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRuntimeDefinition.cs`
- Create: `Assets/Tactics/Scripts/Common/Skills/Graph/SkillExecutionContext.cs`

- [ ] 实现 `SkillGraphRuntimeDefinition.FromAsset(SkillGraphAsset asset)`
- [ ] 标准化运行时节点索引、端口映射、入口缓存
- [ ] 定义 `SkillExecutionContext`：Caster、CurrentNodeId、PrimaryTarget、TargetPoint、TargetSet、Blackboard、StepCount、LastError
- [ ] 定义上下文的最小辅助 API：设置/读取主目标、目标点、目标集合、黑板变量

**验收标准**
- 任意合法 `SkillGraphAsset` 都能生成运行时视图
- 运行时上下文能承载两个样例技能所需状态

### Task 4: 建立 Runner 与节点执行器最小闭环

**目标**：实现逐节点解释执行，并跑通两类代表技能。

**Files:**
- Create: `Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRunner.cs`
- Create: `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/ISkillNodeExecutor.cs`
- Create: `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/SkillNodeExecutionResult.cs`
- Create: `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/SkillNodeExecutorRegistry.cs`
- Create: `Assets/Tactics/Scripts/Common/Skills/Graph/Executors/*.cs`

- [ ] 定义统一执行结果：`Success` / `Failed` / `Waiting` / `Completed` / `Branch:<PortName>`
- [ ] 实现 `SkillGraphRunner`：入口启动、节点调度、下一跳选择、完成/失败/中断
- [ ] 实现运行时保护：最大步数、非法跳转、空入口、执行异常隔离
- [ ] 实现阶段1必需执行器：
  - `Start`
  - `SelectPrimaryTarget`
  - `DashToTarget`
  - `ApplyDamage`
  - `ApplyKnockback`
  - `SelectTargetPoint`
  - `CollectTargetsInArea`
  - `ForEachTarget`
  - `Finish`
- [ ] 复用现有效果语义，避免重写伤害/击退主逻辑

**验收标准**
- Runner 能在运行时顺序跑通图
- 非法图不会把战斗打崩，只会中断当前技能
- 两个样例技能能完整执行

### Task 5: 建立旧战斗系统桥接入口

**目标**：不动成长层的前提下，让现有战斗系统可以加载并执行 `SkillGraph`。

**Files:**
- Create: `Assets/Tactics/Scripts/Common/Units/Abilities/SkillGraphAbilityConfig.cs`
- Create: `Assets/Tactics/Scripts/Common/Units/Abilities/SkillGraphAbilityImpl.cs`
- Modify: `Assets/Tactics/Scripts/Common/Units/Abilities/AbilityConfig.cs`
- Modify: `Assets/Tactics/Scripts/Common/Units/Abilities/GenericAbilityImpl.cs`（仅在需要识别桥接配置时）

- [ ] 设计 `SkillGraphAbilityConfig`：持有 `SkillGraphAsset` 引用 + 展示名 + 图标 + 基础消耗
- [ ] 设计 `SkillGraphAbilityImpl`：保留与现有 `IAbility` 接口兼容的交互入口
- [ ] 将目标选择阶段与图执行阶段衔接起来，保证能挂进现有 Grid / Unit / Ability 选择流程
- [ ] 避免影响旧 `AbilityConfig` 技能路径

**验收标准**
- 现有单位可通过桥接配置执行 SkillGraph 技能
- 旧 AbilityConfig 技能行为无回归

### Task 6: 建立 MCP 最小资产操作入口

**目标**：为 Agent 自动生成提供最小可用的资产创建、读取、校验入口。

**Files:**
- Create: `Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphMcpFacade.cs`
- Create: `.agents/docs/skill-graph-mcp-contract.md`

- [ ] 定义最小 MCP 操作面：CreateGraph / GetGraph / UpsertNode / UpsertEdge / ValidateGraph
- [ ] 为每个入口定义稳定的输入输出结构，优先返回结构化错误而非纯字符串
- [ ] 在文档中记录 `SkillGraphSpec` 的最小字段协议与错误码
- [ ] 保证 Agent 可以走“创建 -> 校验 -> 修复 -> 再校验”的闭环

**验收标准**
- 不开编辑器手工点点点，也能通过结构化输入落出一张合法图
- 校验失败时返回 node/edge 级别的可修复错误

### Task 7: 交付两个代表性样例技能图

**目标**：用真实样例证明阶段1闭环不是空架子。

**Files:**
- Create: `Assets/Tactics/Battle/Abilities/SkillGraphs/ChargeStrike_Lv1.asset`
- Create: `Assets/Tactics/Battle/Abilities/SkillGraphs/AreaBlast_Lv1.asset`

- [ ] 样例 1：冲向目标并造成伤害，命中后击退
- [ ] 样例 2：选点范围伤害
- [ ] 让两个样例都能在编辑器中打开、校验通过、运行时执行通过

**验收标准**
- 两个样例都是项目内真实资产，不是伪代码或演示图
- 二者覆盖阶段1的核心节点与执行路径

## Verification

### 代码级验证

- [ ] 为 `SkillGraphValidation` 添加基础编辑态/运行时校验用例（若项目已有 Editor Tests 体系则放入相应测试目录）
- [ ] 为 `SkillGraphRunner` 的基础顺序执行、非法跳转、最大步数保护添加测试

### Unity 编辑器验证

- [ ] 能通过菜单创建 SkillGraph 资产
- [ ] 能打开编辑器、添加节点、连边、保存、重新加载
- [ ] Validate 能对合法图返回通过，对非法图返回结构化错误

### Play Mode / 手工验证

- [ ] 将 `ChargeStrike_Lv1` 挂到一个测试单位上并在战斗内释放成功
- [ ] 将 `AreaBlast_Lv1` 挂到一个测试单位上并在战斗内释放成功
- [ ] 确认旧 `AbilityConfig` 技能仍可正常释放

## Out of Scope for Phase 1

- `SkillDatabase` / `SkillSystem` / `LearnedSkills` 统一收编
- 技能升级 UI 接入 `SkillGraph`
- 预览场景、角色动画播放、技能特效预览
- 并发分支、自由表达式、通用循环 DSL

## Completion Criteria

阶段1完成的定义：

1. `SkillGraphAsset` 能被编辑器创建和校验
2. `SkillGraphRunner` 能稳定执行最小节点子集
3. `SkillGraphAbilityConfig` 能接入现有战斗技能释放链
4. MCP 最小入口能完成图资产创建/更新/校验闭环
5. 两个代表性样例技能在项目内真实跑通

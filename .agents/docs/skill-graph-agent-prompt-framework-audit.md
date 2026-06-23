# SkillGraph Agent 提示词生成框架审计

> 日期：2026-06-13
> 结论口径：基于当前仓库代码与文档的只读审计，不复述旧计划中的未验证假设。

## Summary

当前 `SkillGraph` 方向已经具备“结构化输入 -> 图资产/MCP 操作 -> 校验 -> 运行时执行 -> Ability 桥接”的主骨架，不再是纯设计稿。

但如果目标定义为“AI agent 能稳定把提示词生成可上线的战斗技能，并完成旧技能体系迁移”，那么框架仍未完成。当前缺口主要不在 Graph 基础设施，而在：

1. `自然语言/提示词 -> SkillGraphSpec` 的稳定编译层
2. 旧技能迁移所需的 projectile / trigger / state 语义收口
3. `validate -> fix -> revalidate` 的自动修复闭环
4. 覆盖真实迁移技能的系统级验证

## 已完成的骨架

### 1. SkillGraph 资产、运行时、编辑器已落地

- 编辑态资产与运行时主类已存在：
  - `SkillGraphAsset`
  - `SkillGraphRuntimeDefinition`
  - `SkillGraphRunner`
  - `SkillExecutionContext`
  - `SkillGraphValidation`
- 编辑器侧已存在：
  - `SkillGraphEditorWindow`
  - `SkillGraphView`
  - `SkillGraphNodeWrapper`
  - `SkillGraphAssetMenu`

说明：这意味着技能图系统已经可以在 Unity 内创建、查看、保存、校验和执行，不是停留在设计文档。

### 2. MCP / Agent 操作门面已落地

`SkillGraphMcpFacade` 已提供的能力包括：

- `CreateGraph`
- `LoadGraph`
- `GetGraphSummary`
- `GetGraphDetail`
- `ListGraphs`
- `GetGraphNodeConnections`
- `UpsertNode`
- `UpsertEdge`
- `RemoveNode`
- `RemoveEdge`
- `ValidateGraph`

此外，`skill-graph-mcp-contract.md` 已定义最小协议、错误码、Phase 2 查询能力、bridge sync audit 与 legacy readiness audit。

### 3. Ability 桥接已落地

当前已存在：

- `SkillGraphAbilityConfig`
- `SkillGraphAbilityImpl`
- `SkillGraphAbilityConfigGenerator`

说明：SkillGraph 已经可以通过桥接配置接入现有技能释放链，而不是只能在孤立测试环境运行。

### 4. 实际图资产与桥接资产已存在

当前仓库中已存在多份图资产和桥接配置，例如：

- `ChargeStrike_Lv1.asset`
- `AreaBlast_Lv1.asset`
- `Fireball_Graph.asset`
- `RangedAttack_Graph.asset`
- `MeleeAttack_Graph.asset`

以及对应的 `*_Ability.asset` 桥接配置。

这说明项目已经进入“真实资产演进”阶段，而不是只有 demo graph。

## 仍未完成的内容

### 1. 缺少独立的“提示词 -> SkillGraphSpec”编译器层

当前落地的是 MCP 契约与门面，不是一个仓库内专门负责把自然语言稳定转成 `SkillGraphSpec` 或节点/边 patch 的编译器。

实际现状更接近：

```text
Agent 自己理解提示词
  -> Agent 手工整理结构化节点/边/参数
  -> 调用 SkillGraphMcpFacade / MCP 接口
```

而不是：

```text
提示词
  -> 受控编译器
  -> SkillGraphSpec
  -> 自动落资产
```

因此，当前“agent 提示词生成”能力更多依赖 agent 本身质量，而不是 repo 内已有一个稳定编译层。

### 2. 旧技能迁移语义没有收口

`SkillGraphLegacyAbilityAudit` 明确表明，目前仍有多类旧技能不能视为“已可安全迁移”：

- `NeedsProjectileSemantic`
  - `RangedAttack`
  - `MagicAttack`
  - `HeavyShot`
  - `Fireball`
- `NeedsManualDesign`
  - `Freeze`
  - `Mark`
  - `Counter`
  - `ChargeHeal`
  - `MeleeHeal`
- `BlockedByLegacyIncompleteImplementation`
  - `ChargeAttack`
  - `Uppercut`

这说明图框架虽然能表达更多节点，但“如何与旧技能真实语义对齐”并未完成，尤其是：

- projectile 发射/飞行/命中/结算时机
- buff / trigger / counter 类触发链
- move-then-heal / stateful 技能的校验与执行语义

### 3. 自动修复闭环未落地

当前 `SkillGraphValidation` 已能返回结构化错误码、分类和修复建议；但仓库里没有看到一个正式的自动修复器，去根据诊断结果直接 patch 图并重试。

现状是：

```text
ValidateGraph
  -> 返回结构化错误
  -> 由 agent / 人工决定如何修
```

而不是：

```text
ValidateGraph
  -> AutoFixGraph
  -> Revalidate
```

因此，“Agent 可稳定批量生成技能图”的最后一公里还没完成。

### 4. 系统级验证仍不足

当前 PlayMode 测试可证明：

- 非法图会在执行前被拦截
- 自疗图可执行
- 单体伤害图可执行

但还不足以证明以下链路已经稳定：

- projectile 技能运行时
- buff / debuff / trigger 语义
- move-then-heal / launch / move skill
- bridge sync 漂移检查后的自动修复
- 旧 `AbilityConfig` 迁移后的真实行为回归

换句话说，当前测试覆盖证明“框架能跑”，还没有证明“迁移完的技能体系可靠”。

### 5. 设计文档中的非目标仍未进入完成态

根据现有设计与实现边界，以下内容仍未完成，且不少仍属于明确的后续阶段内容：

- 成长/升级/存档系统统一接入 `SkillGraph`
- 动画/VFX 预览链路
- 完整 DSL / 任意表达式
- 并发分支与更自由的流程控制
- 全量替换旧 `AbilityConfig`

这些不是当前代码缺失的 bug，而是框架仍然处于阶段化建设中。

## 当前最接近完成态的能力

如果用更保守的表述，当前已经完成的是：

> Agent 可以基于受控结构化输入，借助 MCP / facade 创建、修改、校验并桥接一部分 SkillGraph 技能。

当前还没有完成的是：

> 任意提示词稳定编译为可上线技能，并完成旧技能体系的低风险批量迁移。

## 建议的后续优先级

### P0

- 收口 projectile 语义
- 收口 buff / trigger / heal 类迁移语义
- 为真实迁移技能补运行时回归测试

### P1

- 增加 `validate -> fix -> revalidate` 自动修复层
- 固化 `SkillGraphSpec` 的最小受控输入协议

### P2

- 继续做成长层统一、预览系统、表达能力扩展

## 证据入口

- 设计文档：`.agents/docs/skill-graph-editor-design.md`
- MCP 契约：`.agents/docs/skill-graph-mcp-contract.md`
- 阶段计划：`.agents/plans/skill-graph-editor-phase1-implementation-plan.md`
- MCP 门面：`Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphMcpFacade.cs`
- 迁移审计：`Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphLegacyAbilityAudit.cs`
- 校验器：`Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphValidation.cs`
- 运行时测试：`Assets/Tactics/Tests/PlayMode/SkillGraphRuntimeTests.cs`

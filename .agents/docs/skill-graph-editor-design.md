# SkillGraph 技能图编辑器设计规格

> 状态：Draft
> 日期：2026-06-02
> 范围：技能执行逻辑图编辑器 + Agent/MCP 自动生成链路
> 不含：成长/升级/存档统一、动画/VFX 预览

## 1. 背景

当前项目中的技能系统分为两层：

1. **成长/元技能层** — `SkillDatabase`、`SkillSystem`、`LearnedSkills`
   - 负责技能名、描述、等级、职业、学习/升级规则

2. **执行能力层** — `AbilityConfig`、`TargetingStrategy`、`AbilityEffect`、`GenericAbilityImpl`
   - 负责战斗中的目标选择、效果执行与结算

两层未统一，且对复杂流程型技能（先位移再攻击、命中后分支、多段执行）的表达能力有限。

项目已具备 AI 图编辑器基础设施：
- `AiDecisionGraph`
- `AiDecisionGraphView`
- `AiDecisionGraphEditorWindow`

本规格在不重构成长层的前提下，引入 SkillGraph 体系，优先打通：

**文字描述 → 结构化 SkillGraph → MCP 自动落资产 → 运行时执行**

---

## 2. 目标

1. 建立 `SkillGraphAsset` 作为技能执行逻辑真相源
2. 提供 GraphView 风格技能图编辑器
3. 提供运行时 `SkillGraphRunner` 解释执行
4. 打通 Agent / MCP 自动生成链路
5. 复用现有 `AbilityEffect` / `TargetingStrategy` / `CombatComponent`
6. 支持一个成长层技能条目的不同等级对应多个 `SkillGraph`

---

## 3. 非目标（首版明确不包含）

1. 技能成长/学习/升级/存档系统统一
2. `SkillDatabase` 全量收编
3. 动画播放与技能特效预览
4. 完整 DSL、自由脚本节点、任意表达式语言
5. 一次性替换所有现有 `AbilityConfig`
6. 纯策划手工编辑体验的全面打磨

---

## 4. 总体架构

系统分为 6 层：

### 4.1 成长层（保留现状）
继续使用 `SkillDatabase` / `SkillSystem` / `LearnedSkills`，暂不统一。只负责引用不同等级的 `SkillGraph`。

### 4.2 技能图资产层
`SkillGraphAsset` — 保存技能执行逻辑的编辑态真相源（metadata、节点、边、入口、参数、版本、校验状态）。

### 4.3 运行时轻量视图层
`SkillGraphRuntimeDefinition` — 从图资产生成轻量、标准化运行时定义（节点索引化、边解析、参数标准化、缓存入口与校验结果）。

### 4.4 图执行层
- `SkillExecutionContext` — 单次施法运行时上下文
- `SkillGraphRunner` — 逐节点调度执行
- `ISkillNodeExecutor` — 每种节点类型的执行器

### 4.5 兼容桥接层
SkillGraph 负责流程调度，现有效果系统负责结算。优先复用 `TargetingStrategy`、`AbilityEffect`、`CombatComponent`。

### 4.6 Agent / MCP 生成层
自然语言 → 受控描述协议 → `SkillGraphSpec` → MCP 创建/更新 `SkillGraphAsset` → 校验 → 结构化错误 → 自动修复。

### 4.7 架构关系

```
成长层技能条目（Lv1/Lv2）
  └─ 引用不同的 SkillGraphAsset
       └─ 生成 SkillGraphRuntimeDefinition
            └─ 由 SkillGraphRunner 执行
                 └─ 调度节点执行器
                      └─ 复用 AbilityEffect / TargetingStrategy / CombatComponent

Agent 文本描述
  └─ SkillGraphSpec
       └─ MCP 创建/更新 SkillGraphAsset
```

---

## 5. 资产模型

### 5.1 SkillGraphAsset

| 字段 | 类型 | 说明 |
|------|------|------|
| SkillGraphId | string | 唯一标识 |
| DisplayName | string | 显示名 |
| Version | int | 版本号 |
| Tags | string[] | 标签 |
| EntryNodeId | string | 入口节点 ID |
| SupportedRoleTypes | RoleType[] | 支持的职业 |
| ValidationStatus | enum | 校验状态 |
| Nodes | List\<GraphNodeRecord\> | 节点列表 |
| Edges | List\<GraphEdgeRecord\> | 边列表 |

### 5.2 节点通用字段

| 字段 | 类型 | 说明 |
|------|------|------|
| NodeId | string | 唯一标识 |
| NodeType | enum | 节点类型 |
| Position | Vector2 | 编辑器位置 |
| Enabled | bool | 是否启用 |
| Parameters | dict | 节点参数 |
| Ports | List\<PortDefinition\> | 端口定义 |

### 5.3 边通用字段

| 字段 | 类型 | 说明 |
|------|------|------|
| EdgeId | string | 唯一标识 |
| SourceNodeId | string | 源节点 |
| SourcePort | string | 源端口 |
| TargetNodeId | string | 目标节点 |
| TargetPort | string | 目标端口 |
| ConditionTag | string | 条件标签（可选） |
| Priority | int | 优先级（可选） |

---

## 6. 成长层与 SkillGraph 的关系

采用：**一个成长层技能条目的不同等级 = 多个 SkillGraph**

示例：
- `mage_fireball_1` → `SkillGraph_Fireball_Lv1`
- `mage_fireball_2` → `SkillGraph_Fireball_Lv2`

---

## 7. 节点体系

首版支持 6 类节点。

### 7.1 入口节点
- `Start` — 每图必须且只能有一个，无输入端口，至少一个输出端口

### 7.2 目标选择节点
- `SelectPrimaryTarget` — 选择主目标
- `SelectTargetPoint` — 选择目标点
- `CollectTargetsInArea` — 收集区域内目标
- `CollectTargetsByFilter` — 按条件筛选目标

输出写入上下文：`PrimaryTarget`、`TargetPoint`、`TargetSet`

### 7.3 流程控制节点
- `Sequence` — 顺序执行
- `Branch` — 条件分支
- `CheckCondition` — 条件检查
- `ForEachTarget` — 遍历目标集合（受限，非任意循环）
- `Finish` — 技能成功结束
- `Fail` — 技能失败结束

### 7.4 位移/空间节点
- `MoveCasterToTarget` — 移动施法者到目标
- `MoveCasterToPoint` — 移动施法者到点位
- `DashToTarget` — 冲刺到目标
- `JumpToPoint` — 跳跃到点位
- `ProjectForward` — 沿方向推进
- `SpawnAtPoint` — 在点位生成对象

### 7.5 效果执行节点
- `ApplyDamage` — 造成伤害
- `ApplyHeal` — 治疗
- `ApplyBuff` — 施加 Buff
- `RemoveBuff` — 移除 Buff
- `ApplyKnockback` — 击退
- `ModifyCombatState` — 修改战斗状态

### 7.6 事件节点
- `EmitAnimationCue` — 发出动画事件
- `EmitVfxCue` — 发出特效事件
- `EmitSfxCue` — 发出音效事件
- `EmitGameplayEvent` — 发出通用玩法事件

说明：首版只定义事件挂点，不包含预览系统。

### 7.7 首版不支持
- 任意循环 / 无限循环
- 自由脚本节点
- 任意表达式语言
- 并发执行
- 图内递归子图调用

---

## 8. 运行时执行模型

### 8.1 SkillExecutionContext

单次施法上下文，保存：
- 施法者（Caster）
- 当前技能图引用
- 当前节点（CurrentNode）
- 主目标（PrimaryTarget）
- 目标点（TargetPoint）
- 目标集合（TargetSet）
- 临时变量/黑板（Blackboard）
- 阶段结果
- 执行日志 / 错误状态

### 8.2 SkillGraphRunner

职责：
- 从入口节点启动
- 调度节点执行器
- 根据结果决定下一跳
- 处理成功、失败、中断、超时、非法图

### 8.3 节点执行器

每种节点类型由单独执行器实现：
- 目标选择执行器
- 位移执行器
- 效果执行器
- 分支执行器
- 事件执行器

### 8.4 统一节点结果协议

| 结果 | 说明 |
|------|------|
| Success | 节点执行成功，走默认输出 |
| Failed | 节点执行失败 |
| Waiting | 异步等待中，后续恢复 |
| Completed | 技能成功结束 |
| Branch:\<PortName\> | 走指定分支端口 |

---

## 9. 运行时保护机制

1. **无入口节点** → 禁止运行
2. **非法跳转** → 中止当前技能并上报错误
3. **死循环保护** → 限制单次施法最大执行步数
4. **缺目标/缺点位** → 统一失败或统一默认分支策略
5. **节点执行异常隔离** → 中断当前技能，不影响整场战斗

---

## 10. MCP 契约

### 10.1 图资产操作
- 创建 `SkillGraphAsset`
- 读取图摘要
- 读取整图结构
- 导出结构化 spec

### 10.2 节点操作
- 添加 / 删除 / 更新节点
- 查询节点详情
- 批量添加节点

### 10.3 边操作
- 添加 / 删除 / 更新边
- 批量重连

### 10.4 校验操作
- 执行图校验
- 返回结构化错误与警告

---

## 11. Agent 生成链路

```
自然语言
  → 受控技能描述协议
    → SkillGraphSpec
      → MCP 创建/更新 SkillGraphAsset
        → MCP 校验
          → 返回结构化错误
            → Agent 自动修复
```

不建议：自然语言 → 直接写 Unity 资产

---

## 12. 校验体系

### 12.1 结构校验
- 是否存在且仅存在一个 `Start`
- 是否存在终止节点（`Finish` / `Fail`）
- 是否存在孤立节点
- 边是否连接合法
- 是否缺少必填参数

### 12.2 运行时可执行校验
- 依赖主目标的节点前是否已有目标来源
- 依赖目标点的节点前是否已有点位来源
- `ForEachTarget` 前是否已有目标集合
- 效果节点是否具备执行对象

### 12.3 首版支持域校验
- 是否使用未注册节点类型
- 是否存在不受控循环
- 是否越界到首版不支持的能力

---

## 13. MCP 错误返回格式

每条错误至少包含：

| 字段 | 类型 | 说明 |
|------|------|------|
| code | string | 错误码 |
| severity | enum | Error / Warning |
| nodeId | string | 相关节点（可选） |
| edgeId | string | 相关边（可选） |
| message | string | 人可读描述 |
| suggestedFix | string | 建议修复方式 |

示例错误码：
- `MissingEntryNode`
- `MissingRequiredTarget`
- `InvalidEdgeConnection`
- `UnsupportedNodeType`
- `PotentialInfiniteLoop`
- `UnreachableNode`
- `MissingRequiredParameter`

---

## 14. 风险与控制策略

| 风险 | 控制策略 |
|------|----------|
| 图系统膨胀成万能 DSL | 严控节点种类与流程能力 |
| 与旧 AbilityConfig 长期双轨混乱 | 明确 SkillGraph 是未来主入口，AbilityConfig 是兼容桥 |
| Agent 自动生成成功率不稳定 | 强校验 + 结构化错误返回 + 受控输入协议 |
| 运行时调试成本上升 | 保留执行轨迹与统一日志语义 |

---

## 15. 分阶段落地建议

### 阶段 1：最小闭环
目标：跑通文字描述 → 图资产 → 运行时执行

建议样例技能：
- 冲向目标并造成伤害，命中后击退
- 选点范围伤害

### 阶段 2：稳定化
目标：提高校验质量与 Agent 自动修复成功率

内容：结构化错误增强、增量修图能力、更强参数校验、执行轨迹调试

### 阶段 3：内容扩展
目标：覆盖更多复杂流程技能

内容：新节点、更多受控分支模板、更强集合处理能力、更复杂多段技能支持

---

## 16. 与第二份规格的衔接

第二份"动画/VFX 预览规格"依赖第一份的这些产物：
- 稳定的 `SkillGraph` 节点语义
- 稳定的事件节点模型
- 稳定的运行时执行轨迹
- 稳定的校验规则

推荐顺序：
1. 第一份规格：定义、生成、执行 SkillGraph
2. 第二份规格：基于 SkillGraph 做动画/特效预览与可视调试

---

## 17. 最终结论

第一份规格正式定义为：

**以 `SkillGraphAsset` 为技能执行逻辑真相源、以 Agent/MCP 自动生成链路为首要目标、采用受控节点体系与运行时解释执行模型的技能图编辑器系统。**

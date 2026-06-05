# SkillGraph MCP 契约

> 阶段1最小 MCP 操作入口文档

## 概述

SkillGraph MCP 门面提供以下最小操作：
- `CreateGraph` — 创建新图资产
- `LoadGraph` — 加载已有图资产
- `GetGraphSummary` — 读取图摘要
- `UpsertNode` — 添加或更新节点
- `UpsertEdge` — 添加边
- `RemoveNode` — 删除节点
- `RemoveEdge` — 删除边
- `ValidateGraph` — 执行校验并返回结构化错误

## 节点类型

| NodeType | 说明 | 关键参数 |
|----------|------|----------|
| `Start` | 入口节点 | 无 |
| `SelectPrimaryTarget` | 选择主目标 | `maxRange: int` |
| `SelectTargetPoint` | 选择目标点 | `maxRange: int` |
| `CollectTargetsInArea` | 收集区域内目标 | `radius: int`, `shape: Circle/Cross` |
| `ForEachTarget` | 遍历目标集合 | 无 |
| `DashToTarget` | 冲刺到目标 | `maxRange: int`, `collisionDamage: float` |
| `ApplyDamage` | 造成伤害 | `baseDamage: float`, `damageType: Physical/Magical`, `isRanged: bool`, `canCrit: bool` |
| `ApplyKnockback` | 击退 | `distance: int`, `height: float`, `duration: float` |
| `Branch` | 条件分支 | 无（通过边的 portType 区分 OnTrue/OnFalse） |
| `Finish` | 成功结束 | 无 |
| `Fail` | 失败结束 | 无 |

## 端口类型

| PortType | 说明 |
|----------|------|
| `Default` | 默认输出 |
| `OnTrue` | Branch 节点真分支 |
| `OnFalse` | Branch 节点假分支 |
| `OnHit` | 预留 |
| `OnMiss` | 预留 |

## Agent 生成工作流

```
1. CreateGraph(path, name)
2. UpsertNode(graph, "1", Start, (0,0))
3. UpsertNode(graph, "2", SelectPrimaryTarget, (200,0), {maxRange: 1})
4. UpsertNode(graph, "3", DashToTarget, (400,0), {maxRange: 4, collisionDamage: 1})
5. UpsertNode(graph, "4", ApplyDamage, (600,0), {baseDamage: 10, damageType: Physical})
6. UpsertNode(graph, "5", ApplyKnockback, (800,0), {distance: 1})
7. UpsertNode(graph, "6", Finish, (1000,0))
8. UpsertEdge(graph, "1", "2")
9. UpsertEdge(graph, "2", "3")
10. UpsertEdge(graph, "3", "4")
11. UpsertEdge(graph, "4", "5")
12. UpsertEdge(graph, "5", "6")
13. ValidateGraph(graph)
14. 如果有错误，根据 suggestedFix 修改对应节点/边
15. 再次 ValidateGraph 直到通过
```

## 错误码

| Code | 说明 | 修复建议 |
|------|------|----------|
| `MissingEntryNode` | 缺少 Start 节点 | 添加一个 Start 节点 |
| `MultipleEntryNodes` | 多个 Start 节点 | 只保留一个 |
| `NoTerminalNode` | 缺少 Finish/Fail 节点 | 添加至少一个终止节点 |
| `OrphanNode` | 孤立节点 | 连接或删除 |
| `InvalidEdgeSource` | 边引用不存在的源节点 | 修正或删除边 |
| `InvalidEdgeTarget` | 边引用不存在的目标节点 | 修正或删除边 |
| `SelfReferencingEdge` | 自引用边 | 删除 |
| `EntryNodeHasIncoming` | Start 节点有入边 | 移除入边 |
| `TerminalNodeHasOutgoing` | 终止节点有出边 | 移除出边 |
| `MissingRequiredParameter` | 缺少必填参数 | 补充参数 |
| `UnsupportedNodeType` | 首版不支持的节点类型 | 替换为支持的类型 |
| `MissingTargetSource` | 缺少目标来源 | 在前方添加目标选择节点 |
| `MissingPointSource` | 缺少点位来源 | 在前方添加 SelectTargetPoint |
| `UnreachableNode` | 从入口不可达 | 连接到执行流或删除 |

## Phase 2 已扩展能力

当前 `SkillGraphMcpFacade` 已在阶段1最小能力之上补充了以下 phase2 基础接口：

### Graph Query

- `GetGraphDetail(graphPath)`
- `ListGraphs(folderPath?)`
- `GetGraphNodeConnections(graphPath, nodeId)`

这些接口让 Agent 可以在生成图之外，读取：

- 图路径
- 节点列表与参数
- 边列表与端口类型
- 节点入边 / 出边关系
- 图对应的桥接配置是否存在

### Bridge Sync Audit

- `GetBridgeSyncStatus(graphPath)`
- `ValidateBridge(graphPath)`
- `SyncAbilityConfigFromGraph(graphPath, configPath?, manaCost?, targetRangeOverride?, iconAssetPath?)`

这些接口用于审计和同步：

- `SkillGraphAsset`
- `SkillGraphAbilityConfig`

之间的桥接状态，重点检查：

- bridge config 是否存在
- graph 引用是否一致
- `DisplayName` 是否漂移
- `TargetRange` 是否漂移

### Legacy Readiness Audit

- `ListLegacyAbilityConfigs()`
- `RunLegacyAbilityReadinessAudit()`

这些接口用于对当前旧 `AbilityConfig` 技能做迁移资格审计，输出状态包括：

- `ReadyForMigration`
- `NeedsProjectileSemantic`
- `BlockedByLegacyIncompleteImplementation`
- `NeedsManualDesign`
- `SpecialCase`

## 结构化诊断模型（Phase 2）

`SkillGraphDiagnostic` 已扩展为面向 Agent 的结构化结果，除了基础字段：

- `code`
- `severity`
- `nodeId`
- `edgeId`
- `message`
- `suggestedFix`

还包含：

- `category`
  - `Structure`
  - `Runtime`
  - `Unsupported`
  - `Bridge`
  - `Migration`
- `blocking`
- `relatedNodeIds[]`
- `relatedEdgeIds[]`
- `suggestedFixType`
  - `AddNode`
  - `RemoveEdge`
  - `ReplaceNode`
  - `SetParameter`
  - `ReconnectEdge`
  - `CreateBridge`
  - `SyncBridge`
  - `ReviewLegacyImplementation`
  - `DesignProjectileSemantic`

### Phase 2 新增错误码

| Code | Category | 说明 |
|------|----------|------|
| `ProjectileSemanticMissing` | `Migration` | 该技能在迁移到 Graph 前需要先定义 projectile 语义 |
| `LegacyAbilityNotMigrated` | `Migration` | 旧技能尚未进入 Graph 迁移链路 |
| `BridgeMissing` | `Bridge` | 图缺少对应 `SkillGraphAbilityConfig` |
| `WrongGraphReference` | `Bridge` | bridge config 引用了错误的 graph |
| `TargetRangeDrift` | `Bridge` | bridge 的 `TargetRange` 与图推导结果不一致 |
| `DisplayNameDrift` | `Bridge` | bridge 的 `DisplayName` 与 graph 不一致 |

## Projectile Semantics Design（Phase 2）

### 背景

以下技能不能被视为低风险直接迁移项：

- `RangedAttack`
- `MagicAttack`
- `HeavyShot`
- `Fireball`

原因不是图结构本身复杂，而是它们都依赖 **projectile / 弹道语义**，必须先统一：

1. projectile 是不是图节点语义
2. 伤害是在发射时结算，还是在命中时结算
3. smoke test 如何验证“发射成功 / 命中成功 / 结算成功”

### 设计结论

在 phase2 中，projectile 先按 **“图语义 + 表现事件钩子”** 的折中方式定义，不直接引入完整预览系统。

#### 核心原则

1. **逻辑命中时机以命中后结算为准**
   - `ApplyDamage` 应发生在 projectile 命中后，而不是发射瞬间。

2. **Graph 先描述逻辑阶段，不强制内嵌完整表现实现**
   - 也就是说，先有：
     - 目标选择
     - 发射阶段
     - projectile 旅行阶段
     - 命中阶段
     - 结算阶段
   - 具体视觉表现仍可以通过事件钩子补充。

3. **Smoke test 先验证逻辑命中链，不验证完整视觉表现**
   - phase2 的重点是验证：
     - 图是否能进入 projectile 阶段
     - 是否能到达命中阶段
     - 是否能在命中后结算伤害 / Buff

### 推荐图模板

#### 单体 projectile 模板

```text
Start
-> SelectPrimaryTarget
-> EmitProjectileCue / ProjectileLaunch
-> ProjectileTravel
-> OnHit
-> ApplyDamage
-> Finish
```

适用于：

- `RangedAttack`
- `MagicAttack`
- `HeavyShot`

#### 范围 projectile 模板

```text
Start
-> SelectTargetPoint
-> EmitProjectileCue / ProjectileLaunch
-> ProjectileTravel
-> OnHit
-> CollectTargetsInArea
-> ForEachTarget
-> ApplyDamage
-> Finish
```

适用于：

- `Fireball`

### 对迁移批次的影响

因此，以下技能在 readiness audit 中应统一归类为：

- `NeedsProjectileSemantic`

包括：

- `RangedAttack`
- `MagicAttack`
- `HeavyShot`
- `Fireball`

在 projectile 语义未定清前，这些技能不应进入“低风险直接迁移批次”。

## 推荐迁移批次（修正版）

### Batch 1：低风险

- `MeleeAttack`

### Batch 2：Projectile 语义完成后迁移

- `RangedAttack`
- `MagicAttack`
- `HeavyShot`
- `Fireball`

### Batch 3：状态 / 触发型

- `Freeze`
- `Mark`
- `Counter`

### Batch 4：高风险 / 旧实现不完整

- `ChargeAttack`
- `Uppercut`
- `ChargeHeal`
- `MeleeHeal`

### Special Case

- `Move`

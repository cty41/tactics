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

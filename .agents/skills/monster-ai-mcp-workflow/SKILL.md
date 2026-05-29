---
name: monster-ai-mcp-workflow
description: "Use when creating monster AI assets (AiDecisionGraph, AIProfile, AiBrainAsset) via MCP — defines structured input contract, MCP field mapping, generation workflow, validation, and templates"
---

# Monster AI MCP Workflow

基于 MCP 自动生成怪物 AI 资产的完整工作流。

## Quick Reference

| 步骤 | MCP 工具 | 说明 |
|------|----------|------|
| 1 | `manage_asset(search)` | 防重名检查 |
| 2 | `manage_scriptable_object(create)` | 创建 `AiDecisionGraph` |
| 3 | `manage_scriptable_object(modify)` | 写入节点/边 |
| 4 | `manage_scriptable_object(create)` | 创建 `AIProfile` |
| 5 | `manage_scriptable_object(modify)` | 写入权重/曲线/扰动 |
| 6 | `manage_scriptable_object(create)` | 创建 `AiBrainAsset` |
| 7 | `manage_scriptable_object(modify)` | 绑定 graph/profile + 默认参数 |
| 8 | `refresh_unity` | 刷新资产数据库 |
| 9 | 读取校验 | 静态校验 |

## When to use

- 根据结构化怪物需求自动创建 AI 资产
- 批量创建多个怪物类型的 AI 配置
- 复制/变体已有 AI 资产

## 输入契约

### 结构化需求对象

```json
{
  "monster_name": "GoblinWarrior",
  "style_label": "Aggressive",
  "output_dir": "Assets/Tactics/Arts/ScriptableObjects/MonsterAI",

  "intent_nodes": [
    {
      "node_id": "1",
      "intent_type": "Engage",
      "base_priority": 15
    },
    {
      "node_id": "2",
      "intent_type": "BasicAttack",
      "base_priority": 20
    },
    {
      "node_id": "3",
      "intent_type": "FinishOff",
      "base_priority": 25
    },
    {
      "node_id": "4",
      "intent_type": "Retreat",
      "base_priority": 5
    },
    {
      "node_id": "5",
      "intent_type": "HoldPosition",
      "base_priority": 1
    }
  ],

  "rule_nodes": [
    {
      "node_id": "10",
      "rule_name": "TargetInRange",
      "rule_type": "TargetInMoveAttackRange",
      "parameter": 0
    },
    {
      "node_id": "11",
      "rule_name": "NotLowHealth",
      "rule_type": "HealthAboveThreshold",
      "parameter": 0.2
    }
  ],

  "score_nodes": [
    {
      "node_id": "20",
      "score_name": "DistanceToTarget",
      "score_type": "DistanceToTarget",
      "weight": 5.0,
      "curve": [[0,1,0,0],[1,0,0,0]]
    },
    {
      "node_id": "21",
      "score_name": "KillPotential",
      "score_type": "KillPotential",
      "weight": 8.0,
      "curve": [[0,0,0,0],[1,1,0,0]]
    }
  ],

  "edges": [
    {"source": "1", "target": "10"},
    {"source": "2", "target": "20"},
    {"source": "2", "target": "21"},
    {"source": "3", "target": "10"}
  ],

  "brain_defaults": {
    "low_health_threshold": 0.3,
    "killable_damage_threshold": 0.5,
    "low_health_target_bonus": 20,
    "retreat_base_score": 50
  },

  "profile": {
    "enable_distance_score": true,
    "enable_target_health_score": true,
    "enable_kill_potential_score": true,
    "distance_weight": 5.0,
    "target_health_weight": 3.0,
    "kill_potential_weight": 8.0,
    "noise_factor": 0.05
  }
}
```

### 字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `monster_name` | string | 怪物名称，用于资产文件名 |
| `style_label` | string | AI 风格标签 |
| `output_dir` | string | 输出目录路径 |
| `intent_nodes` | array | 意图节点列表 |
| `rule_nodes` | array | 规则节点列表 |
| `score_nodes` | array | 评分节点列表 |
| `edges` | array | 边列表 (source→target) |
| `brain_defaults` | object | 脑资产默认参数 |
| `profile` | object | 评分风格配置 |

### 枚举值

**IntentType**: `Engage`, `BasicAttack`, `AbilityUse`, `Retreat`, `FinishOff`, `HoldPosition`

**RuleType**: `TargetInRange`, `TargetInMoveAttackRange`, `HealthAboveThreshold`, `HealthBelowThreshold`, `HasAvailableAbility`, `TargetKillable`, `DestinationSafe`, `HasAllyNearby`

**ScoreType**: `DistanceToTarget`, `TargetHealth`, `SelfHealth`, `TargetValue`, `PositionSafety`, `KillPotential`, `AllyProximity`

## MCP Field Mapping

### AiDecisionGraph (ScriptableObject)

| 字段 | MCP patch 路径 | 说明 |
|------|---------------|------|
| `_nodes` | `_nodes` | 节点列表 |
| `_edges` | `_edges` | 边列表 |

#### IntentNodeRecord 子字段

| 字段 | patch 路径 |
|------|-----------|
| `_nodeId` | `_nodes[0]._nodeId` |
| `_position` | `_nodes[0]._position` |
| `_enabled` | `_nodes[0]._enabled` |
| `_intentType` | `_nodes[0]._intentType` |
| `_basePriority` | `_nodes[0]._basePriority` |

#### RuleNodeRecord 子字段

| 字段 | patch 路径 |
|------|-----------|
| `_nodeId` | `_nodes[1]._nodeId` |
| `_ruleName` | `_nodes[1]._ruleName` |
| `_ruleType` | `_nodes[1]._ruleType` |
| `_parameter` | `_nodes[1]._parameter` |
| `_cooldownTurns` | `_nodes[1]._cooldownTurns` |
| `_isOneShot` | `_nodes[1]._isOneShot` |

#### ScoreNodeRecord 子字段

| 字段 | patch 路径 |
|------|-----------|
| `_nodeId` | `_nodes[2]._nodeId` |
| `_scoreName` | `_nodes[2]._scoreName` |
| `_scoreType` | `_nodes[2]._scoreType` |
| `_weight` | `_nodes[2]._weight` |
| `_parameter` | `_nodes[2]._parameter` |

#### GraphEdgeRecord 子字段

| 字段 | patch 路径 |
|------|-----------|
| `_edgeId` | `_edges[0]._edgeId` |
| `_sourceNodeId` | `_edges[0]._sourceNodeId` |
| `_targetNodeId` | `_edges[0]._targetNodeId` |

### AIProfile (ScriptableObject)

| 字段 | patch 路径 |
|------|-----------|
| `_enableDistanceScore` | `_enableDistanceScore` |
| `_distanceWeight` | `_distanceWeight` |
| `_distanceCurve` | `_distanceCurve` |
| `_noiseFactor` | `_noiseFactor` |
| `_styleLabel` | `_styleLabel` |

### AiBrainAsset (ScriptableObject)

| 字段 | patch 路径 |
|------|-----------|
| `_decisionGraph` | `_decisionGraph` (引用 GUID) |
| `_profile` | `_profile` (引用 GUID) |
| `_lowHealthThreshold` | `_lowHealthThreshold` |
| `_killableDamageThreshold` | `_killableDamageThreshold` |
| `_enableVerboseLogging` | `_enableVerboseLogging` |

## Workflow

### Step 1: 防重名检查

```
manage_asset(search) → 检查 {output_dir}/{monster_name}* 是否存在
```

### Step 2: 创建 AiDecisionGraph

```
manage_scriptable_object(create) → AiDecisionGraph
path: {output_dir}/{monster_name}Graph.asset
```

### Step 3: 写入节点和边

```
manage_scriptable_object(modify) → patches:
[
  {"path": "_nodes[0]._nodeId", "value": "1"},
  {"path": "_nodes[0]._intentType", "value": "Engage"},
  {"path": "_nodes[0]._basePriority", "value": 15},
  ...
]
```

**注意**: 节点列表需要逐个 patch，边列表同理。使用 `batch_execute` 批量化。

### Step 4: 创建 AIProfile

```
manage_scriptable_object(create) → AIProfile
path: {output_dir}/{monster_name}Profile.asset
```

### Step 5: 写入评分配置

```
manage_scriptable_object(modify) → patches:
[
  {"path": "_enableDistanceScore", "value": true},
  {"path": "_distanceWeight", "value": 5.0},
  ...
]
```

### Step 6: 创建 AiBrainAsset

```
manage_scriptable_object(create) → AiBrainAsset
path: {output_dir}/{monster_name}Brain.asset
```

### Step 7: 绑定引用和参数

```
manage_scriptable_object(modify) → patches:
[
  {"path": "_decisionGraph", "value": {"guid": "<graph_guid>"}},
  {"path": "_profile", "value": {"guid": "<profile_guid>"}},
  {"path": "_lowHealthThreshold", "value": 0.3},
  ...
]
```

### Step 8: 刷新和校验

```
refresh_unity → 刷新资产数据库
```

### Step 9: 静态校验

读取 `AiDecisionGraph` 资产，校验：
- 节点列表非空
- 边引用的节点都存在
- 意图类型不重复
- 脑资产成功引用 graph 和 profile

## Templates

### 基础近战小怪

```json
{
  "monster_name": "BasicMelee",
  "style_label": "Aggressive",
  "intent_nodes": [
    {"node_id": "1", "intent_type": "Engage", "base_priority": 15},
    {"node_id": "2", "intent_type": "BasicAttack", "base_priority": 20},
    {"node_id": "3", "intent_type": "FinishOff", "base_priority": 25},
    {"node_id": "4", "intent_type": "Retreat", "base_priority": 5},
    {"node_id": "5", "intent_type": "HoldPosition", "base_priority": 1}
  ],
  "rule_nodes": [
    {"node_id": "10", "rule_name": "TargetInRange", "rule_type": "TargetInMoveAttackRange"},
    {"node_id": "11", "rule_name": "NotLowHealth", "rule_type": "HealthAboveThreshold", "parameter": 0.2}
  ],
  "score_nodes": [
    {"node_id": "20", "score_name": "Distance", "score_type": "DistanceToTarget", "weight": 5},
    {"node_id": "21", "score_name": "TargetHP", "score_type": "TargetHealth", "weight": 3},
    {"node_id": "22", "score_name": "KillShot", "score_type": "KillPotential", "weight": 8},
    {"node_id": "23", "score_name": "Safety", "score_type": "PositionSafety", "weight": 4}
  ],
  "edges": [
    {"source": "1", "target": "10"},
    {"source": "2", "target": "20"},
    {"source": "2", "target": "21"},
    {"source": "2", "target": "22"},
    {"source": "3", "target": "10"},
    {"source": "4", "target": "23"}
  ],
  "brain_defaults": {
    "low_health_threshold": 0.3,
    "killable_damage_threshold": 0.5,
    "low_health_target_bonus": 20,
    "retreat_base_score": 50
  },
  "profile": {
    "distance_weight": 5,
    "target_health_weight": 3,
    "kill_potential_weight": 8,
    "position_safety_weight": 4,
    "noise_factor": 0.05,
    "style_label": "Aggressive"
  }
}
```

## Anti-patterns

| ❌ 错误 | ✅ 正确 | 原因 |
|---------|---------|------|
| 自然语言直接作为 patch 参数 | 先归一化为结构化对象 | MCP 需要精确字段路径 |
| 创建后不校验 | 执行静态校验 | 区分"创建成功"和"资产可用" |
| 每次从零拼配置 | 用模板+覆盖 | 效率和一致性 |
| 不防重名 | 先 search 再 create | 避免覆盖已有资产 |
| 单独 patch 每个字段 | 用 batch_execute 批量化 | 减少 MCP 往返 |

## Custom Tool 评估

当前阶段不需要 Unity custom tool。仅在以下情况才考虑：

- `manage_scriptable_object` 对嵌套列表 patch 太脆弱
- 多次 patch 强一致性事务需求
- agent 生成流程错误率过高

## Checklist

- [ ] 结构化输入对象已定义
- [ ] 字段到 MCP patch 路径映射完整
- [ ] 防重名检查已执行
- [ ] 三类资产创建并写入完成
- [ ] 脑资产引用 graph 和 profile 已绑定
- [ ] `refresh_unity` 已调用
- [ ] 静态校验通过

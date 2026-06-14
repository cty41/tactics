# SkillGraph 技能创建向导

> Agent 在 plan 模式下使用本文档，通过提问引导用户完善技能细节，最终自动生成 SkillGraphSpec JSON。

## 工作流

```
用户输入: "冰霜新星, 对周围敌人造成魔法伤害并施加冰冻"
  ↓
Step 1: 意图识别 → 识别技能模式
Step 2: 提问补全 → 覆盖缺失参数
Step 3: 生成 gameplay-test.md → 定义预期行为
Step 4: 生成 SkillGraphSpec JSON → 实现行为
Step 5: 输出待用户确认
```

---

## Step 1: 意图识别

从用户描述中提取以下维度：

| 维度 | 关键词示例 | 默认值 |
|------|-----------|--------|
| **目标类型** | 单体/范围/自身/友军 | 单体 |
| **伤害类型** | 物理/魔法/无 | 物理 |
| **效果类型** | 伤害/治疗/buff/击退/位移 | 伤害 |
| **弹道** | 远程/投射/弹道/射击 | 无 |
| **位移** | 冲锋/突进/跳跃 | 无 |
| **特殊** | 冰冻/标记/反击/嘲讽 | 无 |

### 模式 → 节点链映射

| 模式 | 节点链 |
|------|--------|
| **单体伤害** | Start → SelectPrimaryTarget → ApplyDamage → Finish |
| **单体远程** | Start → SelectPrimaryTarget → ProjectileLaunch → OnHit → ApplyDamage → Finish |
| **范围伤害** | Start → SelectTargetPoint → CollectTargetsInArea → ForEachTarget → ApplyDamage → Finish |
| **自身治疗** | Start → SelectSelf → ApplyHeal → Finish |
| **友军治疗** | Start → SelectAlly → ApplyHeal → Finish |
| **范围治疗** | Start → SelectTargetPoint → CollectTargetsInArea → ForEachTarget → ApplyHeal → Finish |
| **单体+Buff** | Start → SelectPrimaryTarget → ApplyBuff → Finish |
| **伤害+Buff** | Start → SelectPrimaryTarget → ApplyDamage → ApplyBuff → Finish |
| **冲锋+伤害** | Start → SelectPrimaryTarget → DashToTarget → Finish |
| **击退** | Start → SelectPrimaryTarget → ApplyKnockback → Finish |
| **范围+伤害+Buff** | Start → SelectTargetPoint → CollectTargetsInArea → ForEachTarget → ApplyDamage → ApplyBuff → (loop) → Finish |

### 组合技能识别

当描述包含多个效果时，按顺序串联节点：
- "造成伤害**并**施加冰冻" → ApplyDamage → ApplyBuff
- "冲锋**并**造成伤害" → DashToTarget → ApplyDamage
- "治疗**并**施加护盾" → ApplyHeal → ApplyBuff

---

## Step 2: 提问模板

根据意图识别结果，针对缺失信息提问。**只问缺失的，不问已明确的。**

### P0 必问（影响节点链选择）

```
目标选择：
- 这个技能的目标是什么？
  a) 单个敌人
  b) 自己
  c) 友军
  d) 地面位置（范围）
```

```
效果类型：
- 技能的效果是什么？（可多选）
  a) 造成伤害
  b) 治疗
  c) 施加状态/Buff
  d) 击退
  e) 位移（冲锋/跳跃）
```

### P1 按需问（影响参数）

```
伤害参数：
- 伤害类型？（物理/魔法）
- 基础伤害值？（默认 5）
- 是否可暴击？（默认否）
- 是否远程？（默认否）
```

```
范围参数：
- 影响半径？（默认 1）
- 最大施法距离？（默认 3）
```

```
Buff 参数：
- Buff 名称？
- 持续回合数？（默认 2）
- 是否唯一（不可叠加）？（默认是）
```

```
弹道参数：
- 弹道飞行时间？（默认 0.3 秒）
- 弹道速度？（默认 10）
```

```
位移参数：
- 冲锋最大距离？（默认 3）
- 碰撞伤害？（默认 1）
```

---

## Step 3: 生成 gameplay-test.md

根据用户回答，生成测试规格。格式：

```yaml
feature: <技能名>
scenario: <场景描述>
adapter: Skill
setup:
  - kind: createGrid
  - kind: createCell
    alias: casterCell
    x: 0
    y: 0
  - kind: createCell
    alias: targetCell
    x: 1
    y: 0
  - kind: createUnit
    alias: caster
    playerNumber: 0
    health: 10
    maxHealth: 10
    cellAlias: casterCell
  - kind: createUnit
    alias: target
    playerNumber: 1
    health: 10
    maxHealth: 10
    defenceFactor: 0
    cellAlias: targetCell
  - kind: loadSkillGraph
    alias: graph
    graphKind: <匹配的 graphKind>
    baseDamage: <值>
    ...
  - kind: setTurnContext
    currentPlayerNumber: 0
    playableUnitAliases: [caster]
runtimeActions:
  - kind: executeSkillGraph
    graph: graph
    caster: caster
    target: target
assertions:
  - kind: executionStateEquals
    expected: Completed
  - kind: unitHealthEquals
    target: target
    expected: <预期血量>
```

---

## Step 4: 生成 SkillGraphSpec JSON

根据模式映射表，将节点链转换为 SkillGraphSpec：

```json
{
  "DisplayName": "<技能名>",
  "Description": "<描述>",
  "Nodes": [
    { "Id": "start", "Type": "Start" },
    { "Id": "<节点名>", "Type": "<节点类型>", "Parameters": { ... } },
    ...
    { "Id": "finish", "Type": "Finish" }
  ],
  "Edges": [
    { "Source": "start", "Target": "<下一节点>" },
    ...
    { "Source": "<上一节点>", "Target": "finish" }
  ]
}
```

### 参数填充规则

| 节点类型 | 参数 | 来源 |
|---------|------|------|
| SelectPrimaryTarget | minRange, maxRange | 用户回答或默认 1/3 |
| SelectTargetPoint | maxRange | 用户回答或默认 3 |
| CollectTargetsInArea | radius | 用户回答或默认 1 |
| ApplyDamage | baseDamage, damageType | 用户回答 |
| ApplyHeal | healAmount | 用户回答 |
| ApplyBuff | buffName, duration, isUnique | 用户回答 |
| ApplyKnockback | distance | 用户回答或默认 1 |
| DashToTarget | maxRange, collisionDamage | 用户回答 |
| ProjectileLaunch | travelTime, speed | 用户回答或默认 0.3/10 |

---

## Step 5: 输出确认

向用户展示：
1. 识别到的技能模式
2. 生成的 gameplay-test.md 路径
3. 生成的 SkillGraphSpec JSON
4. 需要用户确认后执行 ApplySpec

---

## 完整对话示例

```
用户: "冰霜新星, 对周围敌人造成魔法伤害并施加冰冻"

Agent:
  识别到: 范围伤害 + 施加Buff
  模式: 范围+伤害+Buff

  需要补充几个细节:
  1. 伤害范围半径? (默认 2)
  2. 基础魔法伤害值? (默认 5)
  3. 冰冻持续回合? (默认 2)
  4. 最大施法距离? (默认 3)

用户: "半径2, 伤害5, 冰冻2回合, 距离3"

Agent: 生成中...

  gameplay-test.md:
  [输出 YAML]

  SkillGraphSpec JSON:
  {
    "DisplayName": "FrostNova",
    "Nodes": [
      { "Id": "start", "Type": "Start" },
      { "Id": "point", "Type": "SelectTargetPoint", "Parameters": { "maxRange": 3 } },
      { "Id": "collect", "Type": "CollectTargetsInArea", "Parameters": { "radius": 2 } },
      { "Id": "loop", "Type": "ForEachTarget" },
      { "Id": "damage", "Type": "ApplyDamage", "Parameters": { "baseDamage": 5, "damageType": 1 } },
      { "Id": "buff", "Type": "ApplyBuff", "Parameters": { "buffName": "Frozen", "duration": 2, "isUnique": true } },
      { "Id": "finish", "Type": "Finish" }
    ],
    "Edges": [
      { "Source": "start", "Target": "point" },
      { "Source": "point", "Target": "collect" },
      { "Source": "collect", "Target": "loop" },
      { "Source": "loop", "Target": "damage" },
      { "Source": "damage", "Target": "buff" },
      { "Source": "buff", "Target": "loop" },
      { "Source": "loop", "Target": "finish", "Port": "OnComplete" }
    ]
  }

  确认后执行: ApplySpec + 生成 gameplay-test.md + 编译 plan.json + 运行测试
```

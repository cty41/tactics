# Battle Spec 支持矩阵

> **版本**: v1.0
> **日期**: 2026-06-16
> **状态**: Phase 1 Task 1.1 定版

本文档定义了 Battle adapter 在 `.gameplay-test.md` spec 中支持的所有高层语义。

---

## Setup（初始化）

| kind | adapter | 必填参数 | 说明 |
|------|---------|---------|------|
| `bindBattleController` | Battle | 无 | 绑定 BattleController，注册单位别名（p0_0, p1_0 等）和格子别名 |

---

## Actions（执行）

| kind | adapter | 必填参数 | 说明 |
|------|---------|---------|------|
| `advanceTurn` | Battle | 无 | 推进一回合，自动禁用 AI 自动 Play |
| `endBattleWithResult` | Battle | 无 | 显式结束战斗，触发 BattleEnded 事件 |
| `executeBattleSkillGraph` | Battle | `graphAlias`, `casterAlias` | 通过 SkillGraph 执行技能（新系统入口） |
| `moveUnit` | Battle | `unitAlias` | 移动单位到指定格子（`cellAlias` 或 `x,y`） |
| `setUnitState` | Battle | `unitAlias` | 设置单位状态（health/maxHealth/mana/playerNumber） |
| `addBuff` | Battle | `unitAlias`, `buffName` | 给单位添加 Buff（可选 duration，默认 3） |
| `executeAI` | Battle | `unitAlias` | 执行 AI 决策（需先 createAiBrain） |
| `createAiBrain` | Battle | `brainAssetAlias` | 创建 AI 脑资产（brainType: attack/heal） |
| `loadTestPartyConfig` | Skill | `configPath` | 加载玩家测试队伍配置来源 |
| `loadTestEncounterConfig` | Skill | `configPath` | 加载敌方测试关卡配置来源 |
| `setBattleTestMode` | Skill | `enabled` | 切换战斗测试模式开关 |

### executeBattleSkillGraph 详细参数

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `graphAlias` | string | 是 | SkillGraph 别名（需在 setup 中 createSkillGraph） |
| `casterAlias` | string | 是 | 施法者单位别名 |
| `targetAlias` | string | 否 | 目标单位别名（单体技能） |
| `targetPointAlias` | string | 否 | 目标格子别名（范围技能） |

---

## Assertions（断言）

| kind | adapter | target | expected | 说明 |
|------|---------|--------|----------|------|
| `battleIsActive` | Battle | 无 | boolean | 战斗是否激活 |
| `currentRoundEquals` | Battle | 无 | number | 当前回合数 |
| `unitAliveEquals` | Battle | unit alias | boolean | 单位是否存活（非 Downed） |
| `unitHealthEquals` | Battle | unit alias | number | 单位当前 HP |
| `unitManaEquals` | Battle | unit alias | number | 单位当前 MP |
| `unitMaxHealthEquals` | Battle | unit alias | number | 单位最大 HP |
| `unitHasBuff` | Battle | unit alias | string | 单位是否有指定 Buff |
| `unitBuffDurationEquals` | Battle | unit alias | number | Buff 剩余回合数（需 buffName 参数） |
| `unitPositionEquals` | Battle | unit alias | {x, y} | 单位格子坐标 |
| `unitCanAct` | Battle | unit alias | boolean | 单位是否可行动 |
| `playerNumberEquals` | Battle | unit alias | number | 单位所属玩家号 |
| `unitCountEquals` | Battle | 无 | number | 指定玩家的存活单位数（需 playerNumber 参数） |
| `battleResultEquals` | Battle | 无 | - | 战斗结果（需 winnerPlayerNumber 参数） |
| `aiSelectedIntentTypeEquals` | Battle | 无 | string | AI 最终选择的意图类型 |
| `aiCandidateCountEquals` | Battle | 无 | number | AI 候选动作数 |
| `aiRuleFilteredCountEquals` | Battle | 无 | number | AI 规则过滤数 |
| `cellIsBlocked` | Battle/Skill | cell alias | boolean | 格子是否被占用 |
| `unitOwnerEquals` | Battle/Skill | unit alias | string | 单位归属关系 |
| `unitIsCorpse` | Battle/Skill | unit alias | boolean | 单位是否为尸体 |

---

## 使用示例

### 示例 1：基础回合流

```yaml
feature: Battle
scenario: BattleAdvancesRound
requiredAdapters:
  - Battle
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: advanceTurn
    parameters: {}
  - kind: advanceTurn
    parameters: {}
assertions:
  - kind: battleIsActive
    expected: true
    parameters: {}
  - kind: currentRoundEquals
    expected: 2
    parameters: {}
```

### 示例 2：技能执行 + 结算

```yaml
feature: Battle
scenario: BattleFullCombatVictory
requiredAdapters:
  - Battle
  - Skill
setup:
  - kind: bindBattleController
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: lethalAttackGraph
      graphKind: singleTargetDamage
      baseDamage: 999
actions:
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters:
      graphAlias: lethalAttackGraph
      casterAlias: p1_0
      targetAlias: p2_0
assertions:
  - kind: unitAliveEquals
    target: p2_0
    expected: false
    parameters: {}
  - kind: battleResultEquals
    parameters:
      winnerPlayerNumber: 1
```

### 示例 3：AI 决策

```yaml
feature: Battle
scenario: BattleAiDecision
requiredAdapters:
  - Battle
setup:
  - kind: bindBattleController
    parameters: {}
  - kind: createAiBrain
    parameters:
      brainAssetAlias: attackBrain
      brainType: attack
actions:
  - kind: executeAI
    parameters:
      unitAlias: p1_0
      brainAssetAlias: attackBrain
assertions:
  - kind: aiSelectedIntentTypeEquals
    expected: BasicAttack
    parameters: {}
```

---

## 约束与规则

1. **必须先 bindBattleController**：所有 Battle action/assertion 都依赖 BattleController 绑定
2. **单位别名自动注册**：`bindBattleController` 会按 `p{playerNumber}_{index}` 格式自动注册单位别名
3. **格子别名自动注册**：`bindBattleController` 会按 `cell_{x}_{y}` 格式自动注册格子别名
4. **executeBattleSkillGraph 需要 Skill adapter**：因为 `createSkillGraph` 是 Skill adapter 的 setup action
5. **executeAI 需要先 createAiBrain**：AI 脑资产必须先创建才能使用

---

*文档版本：1.0*
*最后更新：2026-06-16*

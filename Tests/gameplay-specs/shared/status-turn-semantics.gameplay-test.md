---
feature: SharedBattlePrimitives
scenario: StatusTurnSemantics
tags:
  - battle
  - status
contractIds:
  - BUFF-REFRESH-STRATEGY-001
  - BUFF-POISON-SOURCE-001
requiredAdapters:
  - Skill
  - Battle
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createCell
    parameters: { alias: sourceCell, x: 0, y: 0 }
  - kind: createCell
    parameters: { alias: targetCell, x: 1, y: 0 }
  - kind: createCell
    parameters: { alias: stunCell, x: 2, y: 0 }
  - kind: createUnit
    parameters: { alias: source, playerNumber: 0, cellAlias: sourceCell }
  - kind: createUnit
    parameters: { alias: target, playerNumber: 1, cellAlias: targetCell, maxHealth: 30, health: 30 }
  - kind: createUnit
    parameters: { alias: stunTarget, playerNumber: 1, cellAlias: stunCell }
actions:
  - kind: addBuff
    parameters: { unitAlias: target, buffName: BurningA, configAlias: burning_a, duration: 2, effectType: Burning }
  - kind: addBuff
    parameters: { unitAlias: target, buffName: BurningB, configAlias: burning_b, duration: 1, effectType: Burning }
  - kind: addBuff
    parameters: { unitAlias: target, buffName: PoisonA, configAlias: poison_a, duration: 99, effectType: Poison }
  - kind: addBuff
    parameters: { unitAlias: target, buffName: PoisonB, configAlias: poison_b, duration: 1, effectType: Poison }
  - kind: addBuff
    parameters: { unitAlias: stunTarget, buffName: StunA, configAlias: stun_a, duration: 5, effectType: Stun, canAct: false }
  - kind: addBuff
    parameters: { unitAlias: stunTarget, buffName: StunB, configAlias: stun_b, duration: 9, effectType: Stun, canAct: false }
  - kind: tickUnitTurnStart
    parameters: { unitAlias: target }
  - kind: tickUnitTurnEnd
    parameters: { unitAlias: target }
  - kind: tickUnitTurnStart
    parameters: { unitAlias: stunTarget }
assertions:
  - kind: unitHealthEquals
    adapter: Battle
    target: target
    expected: 25
    parameters: {}
  - kind: unitStatusStacksEquals
    target: target
    expected: 2
    parameters: { buffName: BurningA }
  - kind: unitStatusStacksEquals
    target: target
    expected: 1
    parameters: { buffName: PoisonA }
  - kind: unitStatusRemainingActionsEquals
    target: target
    expected: 5
    parameters: { buffName: PoisonA }
  - kind: unitStatusRemainingActionsEquals
    target: stunTarget
    expected: 1
    parameters: { buffName: StunA }
  - kind: unitCanAct
    adapter: Battle
    target: stunTarget
    expected: false
    parameters: {}
timeoutMs: 10000
---

# Shared Battle Primitives - Status Turn Semantics

验证跨来源状态合并、递减点燃、固定毒伤与三回合累加，以及 Stun 重复施加后仍只跳过下一行动。

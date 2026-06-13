---
feature: SkillGraph
scenario: ChargeStrikeStopsBeforeBlockedRetreatAndDamagesBothUnits
tags:
  - mvp
  - skill
  - movement
  - charge
  - damage
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: chargeGraph
      graphKind: charge
      collisionDamage: 1
      maxRange: 3
  - kind: createCell
    parameters:
      alias: casterCell
      x: 0
      'y': 0
  - kind: createCell
    parameters:
      alias: pathCell
      x: 1
      'y': 0
  - kind: createCell
    parameters:
      alias: targetCell
      x: 2
      'y': 0
  - kind: createCell
    parameters:
      alias: blockerCell
      x: 3
      'y': 0
  - kind: createUnit
    parameters:
      alias: caster
      playerNumber: 0
      health: 10
      maxHealth: 10
      cellAlias: casterCell
  - kind: createUnit
    parameters:
      alias: target
      playerNumber: 1
      health: 10
      maxHealth: 10
      cellAlias: targetCell
  - kind: createUnit
    parameters:
      alias: blocker
      playerNumber: 1
      health: 10
      maxHealth: 10
      cellAlias: blockerCell
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases:
        - caster
actions:
  - kind: executeSkillGraph
    parameters:
      graphAlias: chargeGraph
      casterAlias: caster
      primaryTargetAlias: target
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: unitCellEquals
    target: caster
    expected:
      x: 1
      'y': 0
    parameters: {}
  - kind: unitCellEquals
    target: target
    expected:
      x: 2
      'y': 0
    parameters: {}
  - kind: unitHealthEquals
    target: target
    expected: 9
    parameters: {}
  - kind: unitHealthEquals
    target: caster
    expected: 9
    parameters: {}
  - kind: unitHealthEquals
    target: blocker
    expected: 10
    parameters: {}
timeoutMs: 10000
---

# SkillGraph - ChargeStrikeStopsBeforeBlockedRetreatAndDamagesBothUnits

Generated gameplay test spec.

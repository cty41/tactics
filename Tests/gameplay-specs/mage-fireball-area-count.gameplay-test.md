---
feature: SkillGraph
scenario: AreaDamageHitsAllTargetsInRadius
tags:
  - mvp
  - skill
  - damage
  - aoe
  - targetSet
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: areaGraph
      graphKind: areaDamage
      baseDamage: 3
      radius: 1
      maxRange: 4
  - kind: createCell
    parameters:
      alias: casterCell
      x: 0
      'y': 0
  - kind: createCell
    parameters:
      alias: targetPointCell
      x: 1
      'y': 1
  - kind: createCell
    parameters:
      alias: targetCellA
      x: 1
      'y': 0
  - kind: createCell
    parameters:
      alias: targetCellB
      x: 0
      'y': 1
  - kind: createCell
    parameters:
      alias: safeCell
      x: 4
      'y': 4
  - kind: createUnit
    parameters:
      alias: caster
      playerNumber: 0
      health: 10
      maxHealth: 10
      cellAlias: casterCell
  - kind: createUnit
    parameters:
      alias: targetA
      playerNumber: 1
      health: 10
      maxHealth: 10
      defenceFactor: 0
      cellAlias: targetCellA
  - kind: createUnit
    parameters:
      alias: targetB
      playerNumber: 1
      health: 10
      maxHealth: 10
      defenceFactor: 0
      cellAlias: targetCellB
  - kind: createUnit
    parameters:
      alias: safeTarget
      playerNumber: 1
      health: 10
      maxHealth: 10
      defenceFactor: 0
      cellAlias: safeCell
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases:
        - caster
actions:
  - kind: executeSkillGraph
    parameters:
      graphAlias: areaGraph
      casterAlias: caster
      targetPointAlias: targetPointCell
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: unitCountInArea
    expected: 3
    parameters:
      centerAlias: targetPointCell
      radius: 1
  - kind: unitHealthEquals
    target: targetA
    expected: 7
    parameters: {}
  - kind: unitHealthEquals
    target: targetB
    expected: 7
    parameters: {}
  - kind: unitHealthEquals
    target: safeTarget
    expected: 10
    parameters: {}
timeoutMs: 10000
---

# SkillGraph - AreaDamageHitsAllTargetsInRadius

新增 `unitCountInArea` 作为 targetSet 过渡回归。

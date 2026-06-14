---
feature: SkillGraph
scenario: 冰霜新星Skill
tags:
  - mvp
  - skill
  - damage
  - aoe
  - buff
requiredAdapters:
  - Skill
timeoutMs: 10000
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: graph
      graphKind: areaDamage
      baseDamage: 5
      radius: 2
      maxRange: 3
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
      graphAlias: graph
      casterAlias: caster
      targetPointAlias: targetPointCell
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: unitHealthEquals
    target: targetA
    expected: 5
    parameters: {}
  - kind: unitHealthEquals
    target: targetB
    expected: 5
    parameters: {}
  - kind: unitHealthEquals
    target: safeTarget
    expected: 10
    parameters: {}
  - kind: unitHasBuff
    target: targetA
    expected: Frozen
    parameters: {}
  - kind: unitBuffDurationEquals
    target: targetA
    expected: 2
    parameters:
      buffName: Frozen
---

# SkillGraph - 冰霜新星Skill

Generated gameplay test spec.

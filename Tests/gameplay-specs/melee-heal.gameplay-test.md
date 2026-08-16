---
feature: SkillGraph
scenario: AllyHealRestoresFriendlyUnitHealth
tags:
  - mvp
  - skill
  - heal
  - ally
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: allyHealGraph
      graphKind: allyHeal
      healAmount: 4
      maxRange: 1
  - kind: createCell
    parameters:
      alias: casterCell
      x: 0
      'y': 0
  - kind: createCell
    parameters:
      alias: allyCell
      x: 1
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
      alias: ally
      playerNumber: 0
      health: 6
      maxHealth: 10
      cellAlias: allyCell
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases:
        - caster
        - ally
actions:
  - kind: executeSkillGraph
    parameters:
      graphAlias: allyHealGraph
      casterAlias: caster
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: unitHealthEquals
    target: ally
    expected: 10
    parameters: {}
timeoutMs: 10000
---

# SkillGraph - AllyHealRestoresFriendlyUnitHealth

Generated gameplay test spec.

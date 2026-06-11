---
feature: SkillGraph
scenario: SingleTargetDamageReducesTargetHealth
tags:
  - mvp
  - skill
  - damage
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: graph
      graphKind: singleTargetDamage
      baseDamage: 7
  - kind: createUnit
    parameters:
      alias: caster
      playerNumber: 0
      cell:
        x: 0
        'y': 0
  - kind: createUnit
    parameters:
      alias: target
      playerNumber: 1
      health: 10
      maxHealth: 10
      defenceFactor: 0
      cell:
        x: 1
        'y': 0
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
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: unitHealthEquals
    target: target
    expected: 3
    parameters: {}
timeoutMs: 10000
---

# SkillGraph - SingleTargetDamageReducesTargetHealth

Generated gameplay test spec.

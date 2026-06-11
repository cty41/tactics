---
feature: SkillGraph
scenario: SelfHealSkillRaisesCasterHealth
tags:
  - mvp
  - skill
  - heal
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: graph
      graphKind: selfHeal
      healAmount: 4
  - kind: createUnit
    parameters:
      alias: caster
      playerNumber: 0
      health: 6
      maxHealth: 10
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
    target: caster
    expected: 10
    parameters: {}
timeoutMs: 10000
---

# SkillGraph - SelfHealSkillRaisesCasterHealth

Generated gameplay test spec.

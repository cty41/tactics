---
feature: SkillGraph
scenario: UnsupportedAssertionIsRejected
tags:
  - mvp
  - skill
  - validation
requiredAdapters:
  - Skill
timeoutMs: 10000
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
  - kind: visualLooksCorrect
    expected: true
    parameters: {}
---

# SkillGraph - UnsupportedAssertionIsRejected

Generated gameplay test spec.

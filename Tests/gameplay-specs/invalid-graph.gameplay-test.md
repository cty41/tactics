---
feature: SkillGraph
scenario: InvalidGraphWithoutTerminalIsRejected
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
      graphKind: invalidSelfHeal
      healAmount: 5
  - kind: createUnit
    parameters:
      alias: caster
      playerNumber: 0
      health: 10
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
    expected: Aborted
    parameters: {}
  - kind: validationErrorCodeIncludes
    expected: NoTerminalNode
    parameters: {}
---

# SkillGraph - InvalidGraphWithoutTerminalIsRejected

Generated gameplay test spec.

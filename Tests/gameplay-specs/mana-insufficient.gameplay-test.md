---
feature: SkillGraph
scenario: ManaInsufficientPreventsAbilityUse
tags:
  - mvp
  - skill
  - mana
  - ability
  - heal
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
  - kind: createSkillAbilityConfig
    parameters:
      alias: ability
      graphAlias: graph
      manaCost: 8
      targetRange: 1
  - kind: createCell
    parameters:
      alias: casterCell
      x: 0
      y: 0
  - kind: createUnit
    parameters:
      alias: caster
      playerNumber: 0
      health: 6
      maxHealth: 10
      mana: 5
      cellAlias: casterCell
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases:
        - caster
  - kind: createSkillAbility
    parameters:
      alias: abilityImpl
      configAlias: ability
      ownerAlias: caster
  - kind: selectAbility
    parameters:
      abilityAlias: abilityImpl
actions:
  - kind: executeAbilityOnTarget
    target: caster
    parameters:
      abilityAlias: abilityImpl
assertions:
  - kind: executionStateEquals
    expected: Failed
    parameters: {}
  - kind: unitManaEquals
    target: caster
    expected: 5
    parameters: {}
  - kind: lastErrorContains
    expected: Not enough mana
    parameters: {}
  - kind: stepMessageContains
    expected: Failed
    parameters: {}
---

# SkillGraph - ManaInsufficientPreventsAbilityUse

Generated gameplay test spec.

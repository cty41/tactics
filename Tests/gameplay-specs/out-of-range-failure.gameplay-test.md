---
feature: SkillGraph
scenario: TargetOutOfRangePreventsAbilityUse
tags:
  - mvp
  - skill
  - mana
  - ability
  - damage
  - range
requiredAdapters:
  - Skill
timeoutMs: 10000
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: graph
      graphKind: singleTargetDamage
      baseDamage: 7
  - kind: createSkillAbilityConfig
    parameters:
      alias: ability
      graphAlias: graph
      manaCost: 3
      targetRange: 2
  - kind: createCell
    parameters:
      alias: casterCell
      x: 0
      y: 0
  - kind: createCell
    parameters:
      alias: targetCell
      x: 4
      y: 0
  - kind: createUnit
    parameters:
      alias: caster
      playerNumber: 0
      health: 10
      maxHealth: 10
      mana: 10
      cellAlias: casterCell
  - kind: createUnit
    parameters:
      alias: target
      playerNumber: 1
      health: 10
      maxHealth: 10
      cellAlias: targetCell
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
    target: target
    parameters:
      abilityAlias: abilityImpl
assertions:
  - kind: executionStateEquals
    expected: Failed
    parameters: {}
  - kind: unitManaEquals
    target: caster
    expected: 10
    parameters: {}
  - kind: lastErrorContains
    expected: Target out of range
    parameters: {}
  - kind: stepMessageContains
    expected: Failed
    parameters: {}
---

# SkillGraph - TargetOutOfRangePreventsAbilityUse

Generated gameplay test spec.

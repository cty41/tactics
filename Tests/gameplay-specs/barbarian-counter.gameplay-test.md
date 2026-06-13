---
feature: SkillGraph
scenario: CounterBuffRetaliatesWhenDamaged
tags:
  - mvp
  - skill
  - buff
  - counter
  - damage
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: counterGraph
      graphKind: applyBuff
      selectionKind: enemy
      buffName: Counter
      buffEffectType: None
      triggerTiming: DamageTaken
      duration: 2
      maxRange: 1
  - kind: createSkillGraph
    parameters:
      alias: damageGraph
      graphKind: singleTargetDamage
      baseDamage: 4
      canCrit: false
      isRanged: false
  - kind: createCell
    parameters:
      alias: casterCell
      x: 0
      'y': 0
  - kind: createCell
    parameters:
      alias: targetCell
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
      alias: target
      playerNumber: 1
      health: 10
      maxHealth: 10
      defenceFactor: 0
      luck: 0
      cellAlias: targetCell
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases:
        - caster
actions:
  - kind: executeSkillGraph
    parameters:
      graphAlias: counterGraph
      casterAlias: caster
      primaryTargetAlias: target
  - kind: executeSkillGraph
    parameters:
      graphAlias: damageGraph
      casterAlias: caster
      primaryTargetAlias: target
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: unitHasBuff
    target: target
    expected: Counter
    parameters: {}
  - kind: unitBuffDurationEquals
    target: target
    expected: 2
    parameters:
      buffName: Counter
  - kind: unitHealthEquals
    target: target
    expected: 6
    parameters: {}
  - kind: unitHealthEquals
    target: caster
    expected: 9
    parameters: {}
timeoutMs: 10000
---

# SkillGraph - CounterBuffRetaliatesWhenDamaged

Generated gameplay test spec.

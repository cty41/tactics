---
feature: SkillGraph
scenario: MarkedTargetTakesCriticalDamage
tags:
  - mvp
  - skill
  - buff
  - mark
  - damage
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: markGraph
      graphKind: applyBuff
      selectionKind: enemy
      buffName: Marked
      buffEffectType: Marked
      triggerTiming: BeforeAttacked
      duration: 2
      maxRange: 1
  - kind: createSkillGraph
    parameters:
      alias: damageGraph
      graphKind: singleTargetDamage
      baseDamage: 4
      canCrit: true
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
      cellAlias: targetCell
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases:
        - caster
actions:
  - kind: executeSkillGraph
    parameters:
      graphAlias: markGraph
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
    expected: Marked
    parameters: {}
  - kind: unitBuffDurationEquals
    target: target
    expected: 2
    parameters:
      buffName: Marked
  - kind: unitHealthEquals
    target: target
    expected: 2
    parameters: {}
timeoutMs: 10000
---

# SkillGraph - MarkedTargetTakesCriticalDamage

Generated gameplay test spec.

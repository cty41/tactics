---
feature: Necromancer
scenario: CurseAmplifiesIncomingDamage
tags:
  - necromancer
  - curse
  - buff
  - damage
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: curseGraph
      graphKind: applyBuff
      selectionKind: enemy
      buffName: CurseDamageAmplifier
      buffEffectType: CurseDamageAmplifier
      duration: 6
      maxRange: 3
  - kind: createSkillGraph
    parameters:
      alias: damageGraph
      graphKind: singleTargetDamage
      baseDamage: 10
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
      health: 100
      maxHealth: 100
      defenceFactor: 0
      cellAlias: targetCell
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases:
        - caster
actions:
  - kind: executeSkillGraph
    parameters:
      graphAlias: curseGraph
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
    expected: CurseDamageAmplifier
    parameters: {}
  - kind: unitHealthEquals
    target: target
    expected: 87
    parameters: {}
timeoutMs: 10000
---

# Necromancer - CurseAmplifiesIncomingDamage

验证诅咒使目标受到伤害提高 30%（基础伤害 10 * 1.3 = 13，100 - 13 = 87）。

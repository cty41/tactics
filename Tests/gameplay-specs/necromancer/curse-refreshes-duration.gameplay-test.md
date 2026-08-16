---
feature: Necromancer
scenario: CurseRefreshesDurationWithoutStacking
tags:
  - necromancer
  - curse
  - buff
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
      graphAlias: curseGraph
      casterAlias: caster
      primaryTargetAlias: target
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: unitBuffIsUnique
    target: target
    expected: CurseDamageAmplifier
    parameters:
      buffName: CurseDamageAmplifier
  - kind: unitBuffDurationEquals
    target: target
    expected: 12
    parameters:
      buffName: CurseDamageAmplifier
timeoutMs: 10000
---

# Necromancer - CurseRefreshesDurationWithoutStacking

验证同名诅咒重复施放只刷新持续时间（6 + 6 = 12），不叠加。

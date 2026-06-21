---
feature: Necromancer
scenario: CurseReplacesOtherCurseOnSameTarget
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
      alias: curseAGraph
      graphKind: applyBuff
      selectionKind: enemy
      buffName: CurseA
      buffEffectType: CurseDamageAmplifier
      duration: 6
      maxRange: 3
  - kind: createSkillGraph
    parameters:
      alias: curseBGraph
      graphKind: applyBuff
      selectionKind: enemy
      buffName: CurseB
      buffEffectType: CurseDamageAmplifier
      duration: 4
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
      graphAlias: curseAGraph
      casterAlias: caster
      primaryTargetAlias: target
  - kind: executeSkillGraph
    parameters:
      graphAlias: curseBGraph
      casterAlias: caster
      primaryTargetAlias: target
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: unitBuffCountEquals
    target: target
    expected: 1
    parameters:
      buffName: CurseB
  - kind: unitHasBuff
    target: target
    expected: CurseB
    parameters: {}
  - kind: unitBuffDurationEquals
    target: target
    expected: 4
    parameters:
      buffName: CurseB
timeoutMs: 10000
---

# Necromancer - CurseReplacesOtherCurseOnSameTarget

验证不同诅咒后者替换前者：施加 CurseA 后施加 CurseB，目标身上只剩 CurseB。

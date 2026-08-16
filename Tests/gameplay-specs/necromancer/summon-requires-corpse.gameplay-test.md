---
feature: Necromancer
scenario: SummonRequiresCorpse
tags:
  - necromancer
  - summon
  - corpse
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: summonGraph
      graphKind: singleTargetDamage
      baseDamage: 0
      canCrit: false
      isRanged: false
      minRange: 1
      maxRange: 3
  - kind: createCell
    parameters:
      alias: casterCell
      x: 0
      'y': 0
  - kind: createCell
    parameters:
      alias: emptyCell
      x: 2
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
      alias: emptyTarget
      playerNumber: 1
      health: 10
      maxHealth: 10
      cellAlias: emptyCell
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases:
        - caster
actions:
  - kind: executeSkillGraph
    parameters:
      graphAlias: summonGraph
      casterAlias: caster
      primaryTargetAlias: emptyTarget
assertions:
  - kind: unitHealthEquals
    target: emptyTarget
    expected: 10
    parameters: {}
timeoutMs: 10000
---

# Necromancer - SummonRequiresCorpse

验证召唤技能不能对非尸体目标生效（目标未死亡，HP 未变）。

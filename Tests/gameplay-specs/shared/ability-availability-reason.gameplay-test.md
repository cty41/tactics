---
feature: SharedBattlePrimitives
scenario: AbilityAvailabilityReason
tags:
  - battle
  - ability
requiredAdapters:
  - Skill
  - Battle
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createSkillGraph
    parameters: { alias: fireballGraph, graphKind: singleTargetDamage, baseDamage: 4 }
  - kind: createCell
    parameters: { alias: casterCell, x: 0, y: 0 }
  - kind: createUnit
    parameters: { alias: caster, playerNumber: 0, cellAlias: casterCell, maxMana: 10, mana: 0 }
  - kind: createSkillAbilityConfig
    parameters:
      alias: fireballConfig
      graphAlias: fireballGraph
      displayName: Fireball II
      manaCost: 5
      targetRange: 3
  - kind: createSkillAbility
    parameters: { alias: fireballAbility, configAlias: fireballConfig, ownerAlias: caster }
actions:
  - kind: setUnitState
    parameters: { unitAlias: caster, skillId: mage.fireball, skillLevel: 2 }
assertions:
  - kind: abilityAvailabilityEquals
    target: fireballAbility
    expected: DisabledClickable
    parameters: {}
  - kind: abilityAvailabilityReasonEquals
    target: fireballAbility
    expected: 需要 5 点魔法
    parameters: {}
  - kind: actualSkillLevelEquals
    target: caster
    expected: 2
    parameters: { skillId: mage.fireball }
  - kind: unitAbilityListEquals
    target: caster
    expected: [Move, Fireball II]
    parameters: {}
timeoutMs: 10000
---

# Shared Battle Primitives - Ability Availability Reason

验证技能卡可点击禁用状态、稳定原因、角色实际技能等级和运行时能力列表均可自动观察。

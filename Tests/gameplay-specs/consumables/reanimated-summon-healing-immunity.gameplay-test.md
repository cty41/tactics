---
feature: Consumable
scenario: ReanimatedSummonHealingImmunity
tags:
  - battle
  - consumable
  - necromancer
  - healing
requiredAdapters:
  - Map
  - Battle
setup:
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: addConsumableInstance
    adapter: Map
    parameters:
      definitionId: life_potion
      instanceId: reanimated_heal
  - kind: carryConsumableToRosterCharacter
    adapter: Map
    parameters:
      characterId: pure_run_mage
      instanceId: reanimated_heal
  - kind: setUnitState
    adapter: Battle
    parameters:
      unitAlias: p1_0
      characterId: pure_run_mage
      playerNumber: 1
  - kind: setUnitState
    adapter: Battle
    parameters:
      unitAlias: p2_0
      playerNumber: 1
      maxHealth: 20
      health: 2
      isDowned: false
      canReceiveHealing: false
  - kind: moveUnit
    adapter: Battle
    parameters:
      unitAlias: p2_0
      cellAlias: cell_1_0
  - kind: useCarriedConsumable
    adapter: Battle
    parameters:
      casterAlias: p1_0
      targetAlias: p2_0
      characterId: pure_run_mage
assertions:
  - kind: unitCanReceiveHealingEquals
    adapter: Battle
    target: p2_0
    expected: false
    parameters: {}
  - kind: unitHealthEquals
    adapter: Battle
    target: p2_0
    expected: 2
    parameters: {}
  - kind: consumableInstanceExists
    adapter: Map
    target: reanimated_heal
    expected: false
    parameters: {}
timeoutMs: 20000
---

# Consumable - ReanimatedSummonHealingImmunity

不可接受治疗的复活类召唤物仍可成为合法目标；实际治疗为 0，但药水正常成功并消耗。

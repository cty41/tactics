---
feature: Consumable
scenario: LifePotionExplicitAdjacentTarget
tags:
  - battle
  - consumable
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
      instanceId: life_use
  - kind: carryConsumableToRosterCharacter
    adapter: Map
    parameters:
      characterId: pure_run_mage
      instanceId: life_use
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
  - kind: unitHealthEquals
    adapter: Battle
    target: p2_0
    expected: 10
    parameters: {}
  - kind: consumableInstanceExists
    adapter: Map
    target: life_use
    expected: false
    parameters: {}
  - kind: rosterCharacterCarriedConsumableEquals
    adapter: Map
    target: pure_run_mage
    expected: null
    parameters: {}
timeoutMs: 20000
---

# Consumable - LifePotionExplicitAdjacentTarget

角色明确选择正交相邻友军，恢复 8 点生命并立即消耗携带实例。

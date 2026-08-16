---
feature: Consumable
scenario: ConsumableInvalidTargetDoesNotConsume
tags:
  - battle
  - consumable
  - targeting
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
      instanceId: invalid_target_item
  - kind: carryConsumableToRosterCharacter
    adapter: Map
    parameters:
      characterId: pure_run_mage
      instanceId: invalid_target_item
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
      cellAlias: cell_1_1
  - kind: useCarriedConsumable
    adapter: Battle
    parameters:
      casterAlias: p1_0
      targetAlias: p2_0
      characterId: pure_run_mage
      expectSuccess: false
assertions:
  - kind: unitHealthEquals
    adapter: Battle
    target: p2_0
    expected: 2
    parameters: {}
  - kind: consumableInstanceExists
    adapter: Map
    target: invalid_target_item
    expected: true
    parameters: {}
  - kind: rosterCharacterCarriedConsumableEquals
    adapter: Map
    target: pure_run_mage
    expected: invalid_target_item
    parameters: {}
timeoutMs: 20000
---

# Consumable - ConsumableInvalidTargetDoesNotConsume

对角目标不合法，真实能力拒绝执行，效果不发生且实例不消耗。

---
feature: Map
scenario: BattleDeathConsumableRetained
tags:
  - map
  - battle
  - death
  - inventory
requiredAdapters:
  - Map
  - Battle
setup:
  - kind: loadRoguelikeMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - kind: bindBattleController
    parameters: {}
  - kind: setRosterCharacterState
    adapter: Map
    parameters:
      characterId: warrior
      currentHp: 7
      currentMp: 4
      isDead: false
  - kind: addInventoryItem
    adapter: Map
    parameters:
      itemId: potion_01
actions:
  - kind: setUnitState
    adapter: Battle
    parameters:
      unitAlias: p1_0
      characterId: warrior
      playerNumber: 0
      health: 0
      mana: 0
      isDowned: true
  - kind: endBattleWithResult
    adapter: Battle
    parameters:
      winnerPlayerNumber: 2
assertions:
  - kind: rosterCharacterDeadEquals
    adapter: Map
    target: warrior
    expected: true
    parameters: {}
  - kind: inventoryContains
    adapter: Map
    expected: potion_01
    parameters: {}
timeoutMs: 20000
---

# Map - BattleDeathConsumableRetained

战后消耗品保留回归：角色死亡后，背包中的消耗品仍然保留在 PlayerAdventureState.Inventory。

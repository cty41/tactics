---
feature: Map
scenario: BattleDeathLoadoutAutoUnloaded
tags:
  - map
  - battle
  - death
  - loadout
requiredAdapters:
  - Map
  - Battle
setup:
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - kind: bindBattleController
    parameters: {}
  - kind: setRosterCharacterState
    adapter: Map
    parameters:
      characterId: pure_run_mage
      currentHp: 7
      currentMp: 4
      isDead: false
      equipmentSlot: Weapon
      equipmentId: sword_01
actions:
  - kind: addConsumableInstance
    adapter: Map
    parameters:
      definitionId: life_potion
      instanceId: death_loadout_item
  - kind: carryConsumableToRosterCharacter
    adapter: Map
    parameters:
      characterId: pure_run_mage
      instanceId: death_loadout_item
  - kind: setUnitState
    adapter: Battle
    parameters:
      unitAlias: p1_0
      characterId: pure_run_mage
      playerNumber: 0
      health: 0
      mana: 0
      isDowned: true
  - kind: endBattleWithResult
    adapter: Battle
    parameters:
      winnerPlayerNumber: 2
      applyRoguelikeWriteback: true
assertions:
  - kind: rosterCharacterDeadEquals
    adapter: Map
    target: pure_run_mage
    expected: true
    parameters: {}
  - kind: rosterCharacterEquipmentEquals
    adapter: Map
    target: pure_run_mage
    expected: null
    parameters:
      equipmentSlot: Weapon
  - kind: inventoryContains
    adapter: Map
    expected: sword_01
    parameters: {}
  - kind: rosterCharacterCarriedConsumableEquals
    adapter: Map
    target: pure_run_mage
    expected: null
    parameters: {}
  - kind: backpackConsumableCountEquals
    adapter: Map
    expected: 1
    parameters:
      definitionId: life_potion
timeoutMs: 20000
---

# Map - BattleDeathLoadoutAutoUnloaded

战斗结算回写死亡后，装备和携带药水都自动回到各自共享背包，角色装载清空。

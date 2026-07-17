---
feature: Map
scenario: MapEventDeathLoadoutAutoUnloaded
tags:
  - map
  - event
  - death
  - loadout
requiredAdapters:
  - Map
setup:
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - kind: setRosterCharacterState
    parameters:
      characterId: pure_run_mage
      currentHp: 5
      isDead: false
      equipmentSlot: Weapon
      equipmentId: sword_01
actions:
  - kind: addConsumableInstance
    parameters:
      definitionId: mana_potion
      instanceId: event_death_item
  - kind: carryConsumableToRosterCharacter
    parameters:
      characterId: pure_run_mage
      instanceId: event_death_item
  - kind: applyEventResult
    parameters:
      resultType: Damage
      targetType: Self
      selfCharacterId: pure_run_mage
      partyCharacterIds:
        - pure_run_mage
      amount: 99
assertions:
  - kind: rosterCharacterDeadEquals
    target: pure_run_mage
    expected: true
    parameters: {}
  - kind: rosterCharacterEquipmentEquals
    target: pure_run_mage
    expected: null
    parameters:
      equipmentSlot: Weapon
  - kind: inventoryContains
    expected: sword_01
    parameters: {}
  - kind: rosterCharacterCarriedConsumableEquals
    target: pure_run_mage
    expected: null
    parameters: {}
  - kind: backpackConsumableCountEquals
    expected: 1
    parameters:
      definitionId: mana_potion
timeoutMs: 10000
---

# Map - MapEventDeathLoadoutAutoUnloaded

地图事件伤害让角色死亡后，同一结果应用事务立即清空装载并保存，无需等待战斗或重载。

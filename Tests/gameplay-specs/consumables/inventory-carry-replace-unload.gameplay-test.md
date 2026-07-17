---
feature: Map
scenario: InventoryCarryReplaceUnload
tags:
  - consumable
  - inventory
  - loadout
requiredAdapters:
  - Map
setup:
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
actions:
  - kind: addConsumableInstance
    parameters:
      definitionId: life_potion
      instanceId: life_a
  - kind: addConsumableInstance
    parameters:
      definitionId: mana_potion
      instanceId: mana_b
  - kind: carryConsumableToRosterCharacter
    parameters:
      characterId: pure_run_mage
      instanceId: life_a
  - kind: carryConsumableToRosterCharacter
    parameters:
      characterId: pure_run_mage
      instanceId: mana_b
  - kind: unloadRosterCharacterConsumable
    parameters:
      characterId: pure_run_mage
assertions:
  - kind: rosterCharacterCarriedConsumableEquals
    target: pure_run_mage
    expected: null
    parameters: {}
  - kind: backpackConsumableCountEquals
    expected: 2
    parameters: {}
  - kind: consumableInstanceExists
    target: life_a
    expected: true
    parameters: {}
  - kind: consumableInstanceExists
    target: mana_b
    expected: true
    parameters: {}
timeoutMs: 10000
---

# Consumable - InventoryCarryReplaceUnload

两个独立实例依次携带、一步替换再卸下后，都回到共享背包，角色携带槽为空。

---
feature: Map
scenario: InventoryEquipmentOneStepReplace
tags:
  - equipment
  - inventory
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
      isDead: false
      equipmentSlot: Weapon
      equipmentId: staff_01
  - kind: addInventoryItem
    parameters:
      itemId: sword_01
actions:
  - kind: equipInventoryEquipmentToRosterCharacter
    parameters:
      characterId: pure_run_mage
      equipmentId: sword_01
assertions:
  - kind: rosterCharacterEquipmentEquals
    target: pure_run_mage
    expected: sword_01
    parameters:
      equipmentSlot: Weapon
  - kind: inventoryContains
    expected: staff_01
    parameters: {}
timeoutMs: 10000
---

# Equipment - InventoryEquipmentOneStepReplace

从背包装备同槽武器时，一步替换并把旧武器追加回共享背包。

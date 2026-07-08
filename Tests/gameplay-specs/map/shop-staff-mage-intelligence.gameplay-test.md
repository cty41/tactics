---
feature: Map
scenario: ShopStaffMageIntelligence
tags:
  - map
  - shop
  - equipment
  - non-generic-value
requiredAdapters:
  - Map
setup:
  - kind: setAdventureGold
    parameters:
      amount: 20
actions:
  - kind: buyShopEquipment
    adapter: Map
    parameters:
      equipmentId: staff_01
      price: 9
  - kind: equipInventoryEquipmentToRosterCharacter
    adapter: Map
    parameters:
      characterId: mage
      equipmentId: staff_01
assertions:
  - kind: runGoldEquals
    adapter: Map
    expected: 11
    parameters: {}
  - kind: rosterCharacterEquipmentEquals
    adapter: Map
    target: mage
    expected: staff_01
    parameters:
      equipmentSlot: Weapon
  - kind: rosterCharacterTotalAttributeEquals
    adapter: Map
    target: mage
    expected: 12
    parameters:
      attribute: Intelligence
timeoutMs: 10000
---

# Map - ShopStaffMageIntelligence

商店非泛用价值最小回归：购买 `staff_01` 并装备给 `Mage` 后，其总智力应从 7 提升到 12。

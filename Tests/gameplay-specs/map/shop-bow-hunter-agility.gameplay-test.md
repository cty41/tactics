---
feature: Map
scenario: ShopBowHunterAgility
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
      equipmentId: bow_01
      price: 9
  - kind: equipInventoryEquipmentToRosterCharacter
    adapter: Map
    parameters:
      characterId: hunter
      equipmentId: bow_01
assertions:
  - kind: runGoldEquals
    adapter: Map
    expected: 11
    parameters: {}
  - kind: rosterCharacterEquipmentEquals
    adapter: Map
    target: hunter
    expected: bow_01
    parameters:
      equipmentSlot: Weapon
  - kind: rosterCharacterTotalAttributeEquals
    adapter: Map
    target: hunter
    expected: 11
    parameters:
      attribute: Agility
timeoutMs: 10000
---

# Map - ShopBowHunterAgility

商店非泛用价值镜像回归：购买 `bow_01` 并装备给 `Hunter` 后，其总敏捷应从 7 提升到 11。

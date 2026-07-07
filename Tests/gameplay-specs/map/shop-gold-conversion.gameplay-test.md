---
feature: Map
scenario: ShopGoldConversion
tags:
  - map
  - shop
  - gold
requiredAdapters:
  - Map
setup:
  - kind: setAdventureGold
    parameters:
      amount: 10
actions:
  - kind: buyShopEquipment
    adapter: Map
    parameters:
      equipmentId: sword_01
      price: 4
assertions:
  - kind: runGoldEquals
    adapter: Map
    expected: 6
    parameters: {}
  - kind: inventoryContains
    adapter: Map
    expected: sword_01
    parameters: {}
timeoutMs: 10000
---

# Map - ShopGoldConversion

最小商店回归：购买装备后，Gold 从冒险状态中扣减，且装备进入库存。

---
feature: Map
scenario: EventResultItemInventory
tags:
  - map
  - event
  - unified-result
  - item
requiredAdapters:
  - Map
setup: []
actions:
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Item
      targetType: All
      itemId: potion_healing
assertions:
  - kind: inventoryContains
    adapter: Map
    expected: potion_healing
    parameters: {}
timeoutMs: 10000
---

# Map - EventResultItemInventory

直接验证 `EventResult.Item -> RewardResult.ItemIds -> PlayerAdventureState.Inventory` 的统一结果链。

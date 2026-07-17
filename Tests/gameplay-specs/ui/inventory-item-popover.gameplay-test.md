---
feature: UI
scenario: InventoryItemPopover
tags:
  - ui
  - inventory
  - consumable
requiredAdapters:
  - Map
  - UI
setup:
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
actions:
  - kind: addConsumableInstance
    adapter: Map
    parameters:
      definitionId: life_potion
      instanceId: ui_life
  - kind: openUI
    adapter: UI
    parameters:
      uiId: Inventory
  - kind: clickElement
    adapter: UI
    parameters:
      elementName: InventoryFilterConsumable
  - kind: clickElement
    adapter: UI
    parameters:
      elementName: StorageSlot_0
assertions:
  - kind: elementVisible
    adapter: UI
    target: InventoryItemPopover
    expected: true
    parameters: {}
  - kind: elementText
    adapter: UI
    target: InventoryItemName
    expected: 生命药剂
    parameters: {}
  - kind: elementText
    adapter: UI
    target: InventoryItemActionButton
    expected: 携带
    parameters: {}
timeoutMs: 15000
---

# UI - InventoryItemPopover

背包切到消耗品筛选后，单击实例打开锚定详情与操作 popover。

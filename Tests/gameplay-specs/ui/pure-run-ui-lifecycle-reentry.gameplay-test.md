---
feature: UI
scenario: PureRunUiLifecycleReentry
tags: [ui, pure-run, lifecycle, reentry, inventory]
requiredAdapters: [Map, UI]
setup:
  - kind: loadPureRunMap
    adapter: Map
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
actions:
  - kind: openUI
    adapter: UI
    parameters: { uiId: Inventory }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: InventoryFilterEquipment }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: CloseButton }

  - kind: addConsumableInstance
    adapter: Map
    parameters:
      definitionId: life_potion
      instanceId: lifecycle_life_potion

  - kind: openUI
    adapter: UI
    parameters: { uiId: Inventory }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: InventoryFilterConsumable }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: StorageSlot_0 }
  - kind: pressKey
    adapter: UI
    parameters: { key: Escape }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: CloseButton }

  - kind: openUI
    adapter: UI
    parameters: { uiId: Inventory }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: InventoryFilterConsumable }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: CloseButton }

  - kind: openUI
    adapter: UI
    parameters: { uiId: Inventory }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: InventoryFilterConsumable }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: StorageSlot_0 }
assertions:
  - kind: elementText
    adapter: UI
    target: CharacterNameLabel
    expected: Mage
    parameters: {}
  - kind: elementText
    adapter: UI
    target: LevelLabel
    expected: Lv.1
    parameters: {}
  - kind: elementText
    adapter: UI
    target: InventoryItemName
    expected: 生命药剂
    parameters: {}
  - kind: elementExists
    adapter: UI
    target: CloseButton
    expected: true
    parameters: {}
timeoutMs: 20000
---

# UI - Pure Run UI Lifecycle Reentry

同一个 Inventory 实例连续关闭并重新打开三次；隐藏期间加入的消耗品必须在新 VisualElement 树中显示，角色信息、筛选、弹窗、Escape 和关闭按钮保持可用。

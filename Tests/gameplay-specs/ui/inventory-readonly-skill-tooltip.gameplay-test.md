---
feature: UI
scenario: InventoryReadonlySkillTooltip
tags: [ui, inventory, skill]
requiredAdapters: [Map, UI]
setup:
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
actions:
  - kind: setRosterCharacterState
    adapter: Map
    parameters:
      characterId: pure_run_mage
      skillId: mage.fireball
      skillLevel: 2
  - kind: setRosterCharacterState
    adapter: Map
    parameters:
      characterId: pure_run_mage
      skillId: amazon.pickup_spear
      skillType: ExtraUtility
      skillLevel: 1
  - kind: openUI
    adapter: UI
    parameters: { uiId: Inventory }
  - kind: refreshInventory
    adapter: UI
    parameters: {}
  - kind: clickElement
    adapter: UI
    parameters: { elementName: InventorySkillSlot_1 }
assertions:
  - kind: elementText
    adapter: UI
    target: InventorySkillLabel_1
    expected: "火球术\nLv.2"
    parameters: {}
  - kind: elementText
    adapter: UI
    target: InventoryItemName
    expected: 火球术
    parameters: {}
  - kind: elementText
    adapter: UI
    target: InventoryItemMeta
    expected: 主动 · Lv.2
    parameters: {}
  - kind: elementVisible
    adapter: UI
    target: InventoryItemActionButton
    expected: false
    parameters: {}
  - kind: elementExists
    adapter: UI
    target: InventorySkillSlot_2
    expected: true
    parameters: {}
timeoutMs: 15000
---

# UI - Inventory Readonly Skill Tooltip

地图背包按学习顺序显示正常主动/被动技能，隐藏 ExtraUtility，并只提供无操作按钮的详情弹窗。

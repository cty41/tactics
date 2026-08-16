---
feature: UI
scenario: AmazonMultiStabUiFlow
tags: [ui, battle, amazon, ordered-target]
requiredAdapters: [Battle, UI]
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: bindPureRunAbilityToUnit
    adapter: Battle
    parameters: { unitAlias: p1_0, skillId: amazon.multi_stab, level: 1 }
  - kind: setUnitState
    adapter: Battle
    parameters: { unitAlias: p1_0, maxMana: 20, mana: 20 }
  - kind: openUI
    adapter: UI
    parameters: { uiId: Battle }
  - kind: clickBattleUnit
    adapter: Battle
    parameters: { unitAlias: p1_0 }
  - kind: setUnitFacing
    adapter: Battle
    parameters: { unitAlias: p1_0, facing: East }
  - kind: refreshBattleActions
    adapter: UI
    parameters: {}
  - kind: clickElement
    adapter: UI
    parameters: { elementName: AbilityCard_连续刺击_Lv1 }
  - kind: clickBattleUnit
    adapter: Battle
    parameters: { unitAlias: p2_0, expectedOrderedCount: 1 }
  - kind: clickBattleUnit
    adapter: Battle
    parameters: { unitAlias: p2_0, expectedOrderedCount: 2 }
  - kind: refreshBattleActions
    adapter: UI
    parameters: {}
  - kind: rightClickElement
    adapter: UI
    parameters: { elementName: OrderedSelectionPanel }
  - kind: pressKey
    adapter: UI
    parameters: { elementName: OrderedSelectionPanel, key: Escape }
  - kind: pressKey
    adapter: UI
    parameters: { elementName: OrderedSelectionPanel, key: Escape }
assertions:
  - kind: targetMarkerOrderEquals
    adapter: UI
    target: OrderedTargetMarkerRoot
    expected: []
    parameters: {}
  - kind: elementVisible
    adapter: UI
    target: OrderedSelectionPanel
    expected: false
    parameters: {}
timeoutMs: 15000
---

# UI - Amazon Multi Stab UI Flow

真实技能卡进入多段选择，重复目标保留编号，右键和 Esc 按栈撤销，队列空后再次取消退出。

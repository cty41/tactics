---
feature: UI
scenario: BattleUiTwoRowLayout
tags: [ui, battle, layout]
requiredAdapters: [Battle, UI]
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: openUI
    adapter: UI
    parameters: { uiId: Battle }
assertions:
  - kind: elementChildOrderEquals
    adapter: UI
    target: ActionPanel
    expected: [MoveButton, BattleConsumableButton]
    parameters: {}
  - kind: elementRectRelationEquals
    adapter: UI
    target: SkillPanel
    expected: below
    parameters: { otherElement: ActionPanel, tolerance: 1 }
  - kind: elementRectRelationEquals
    adapter: UI
    target: SkillPanel
    expected: nonoverlapping
    parameters: { otherElement: ActionPanel }
timeoutMs: 15000
---

# UI - Battle UI Two Row Layout

移动和独立消耗品位于上排，技能卡组位于下排且矩形不重叠。

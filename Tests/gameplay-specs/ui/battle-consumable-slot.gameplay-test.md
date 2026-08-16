---
feature: UI
scenario: BattleConsumableSlot
tags:
  - ui
  - battle
  - consumable
requiredAdapters:
  - Battle
  - UI
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: openUI
    adapter: UI
    parameters:
      uiId: Battle
assertions:
  - kind: elementExists
    adapter: UI
    target: MoveButton
    expected: true
    parameters: {}
  - kind: elementExists
    adapter: UI
    target: BattleConsumableButton
    expected: true
    parameters: {}
  - kind: elementExists
    adapter: UI
    target: SkillPanel
    expected: true
    parameters: {}
  - kind: elementText
    adapter: UI
    target: BattleConsumableName
    expected: 空
    parameters: {}
timeoutMs: 15000
---

# UI - BattleConsumableSlot

战斗底部结构包含同排的 Move 与独立消耗品槽，技能卡组保留在第二行 SkillPanel。

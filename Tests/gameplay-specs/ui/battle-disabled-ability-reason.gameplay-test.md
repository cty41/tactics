---
feature: UI
scenario: BattleDisabledAbilityReason
tags: [ui, battle, amazon]
requiredAdapters: [Battle, UI]
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: dropAmazonSpear
    adapter: Battle
    parameters: { unitAlias: p1_0, cellAlias: cell_0_1 }
  - kind: openUI
    adapter: UI
    parameters: { uiId: Battle }
  - kind: bindPureRunAbilityToUnit
    adapter: Battle
    parameters: { unitAlias: p1_0, skillId: amazon.poison_spear, level: 1 }
  - kind: clickBattleUnit
    adapter: Battle
    parameters: { unitAlias: p1_0 }
  - kind: refreshBattleActions
    adapter: UI
    parameters: {}
  - kind: clickElement
    adapter: UI
    parameters: { elementName: AbilityCard_毒矛_Lv1 }
assertions:
  - kind: abilityCardAvailabilityEquals
    adapter: UI
    target: AbilityCard_毒矛_Lv1
    expected: DisabledClickable
    parameters: {}
  - kind: elementVisible
    adapter: UI
    target: AbilityReasonTooltip
    expected: true
    parameters: {}
  - kind: elementText
    adapter: UI
    target: AbilityReasonTooltip
    expected: 需要先回收长矛
    parameters: {}
timeoutMs: 15000
---

# UI - Battle Disabled Ability Reason

落矛后毒矛保持可点击置灰，点击显示稳定禁用原因。

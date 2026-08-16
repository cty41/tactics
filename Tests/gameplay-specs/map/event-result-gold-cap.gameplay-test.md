---
feature: Map
scenario: EventResultGoldCap
tags:
  - map
  - event
  - unified-result
  - gold
  - cap
requiredAdapters:
  - Map
setup:
  - kind: setAdventureGold
    parameters:
      amount: 48
actions:
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Gold
      targetType: All
      amount: 10
assertions:
  - kind: runGoldEquals
    adapter: Map
    expected: 50
    parameters: {}
timeoutMs: 10000
---

# Map - EventResultGoldCap

验证统一结果层写回金币时仍遵守 `RunGoldManager.MaxGold = 50` 的上限语义。

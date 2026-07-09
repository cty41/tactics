---
feature: Map
scenario: EventResultAllHeal
tags:
  - map
  - event
  - unified-result
  - heal
  - all
requiredAdapters:
  - Map
setup:
  - kind: setRosterCharacterState
    parameters:
      characterId: warrior
      currentHp: 6
  - kind: setRosterCharacterState
    parameters:
      characterId: mage
      currentHp: 5
actions:
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Heal
      targetType: All
      amount: 4
assertions:
  - kind: rosterCharacterHpEquals
    adapter: Map
    target: warrior
    expected: 10
    parameters: {}
  - kind: rosterCharacterHpEquals
    adapter: Map
    target: mage
    expected: 9
    parameters: {}
timeoutMs: 10000
---

# Map - EventResultAllHeal

直接验证 `EventResult.Heal(All)` 会通过统一结果层治疗整队目标。

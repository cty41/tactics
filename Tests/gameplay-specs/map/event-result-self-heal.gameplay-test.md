---
feature: Map
scenario: EventResultSelfHeal
tags:
  - map
  - event
  - unified-result
  - heal
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
      targetType: Self
      selfCharacterId: warrior
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
    expected: 5
    parameters: {}
timeoutMs: 10000
---

# Map - EventResultSelfHeal

直接验证事件结果的 `Self` 目标语义：只治疗 `selfCharacterId` 指向的角色，不影响其他队友。

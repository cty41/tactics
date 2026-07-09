---
feature: Map
scenario: EventResultRandomAllyDamage
tags:
  - map
  - event
  - unified-result
  - random-ally
  - damage
requiredAdapters:
  - Map
setup:
  - kind: setRosterCharacterState
    parameters:
      characterId: warrior
      currentHp: 10
  - kind: setRosterCharacterState
    parameters:
      characterId: mage
      currentHp: 9
actions:
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Damage
      targetType: RandomAlly
      selfCharacterId: warrior
      partyCharacterIds:
        - warrior
        - mage
      amount: 3
assertions:
  - kind: rosterCharacterHpEquals
    adapter: Map
    target: warrior
    expected: 10
    parameters: {}
  - kind: rosterCharacterHpEquals
    adapter: Map
    target: mage
    expected: 6
    parameters: {}
timeoutMs: 10000
---

# Map - EventResultRandomAllyDamage

直接验证 `RandomAlly` 不会错误命中 `selfCharacterId`，而是命中其他队友。

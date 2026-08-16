---
feature: Map
scenario: EventResultPersistenceWriteback
tags:
  - map
  - event
  - unified-result
  - persistence
requiredAdapters:
  - Map
setup:
  - kind: setRosterCharacterState
    parameters:
      characterId: warrior
      currentHp: 6
actions:
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Item
      targetType: All
      itemId: potion_healing
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Buff
      targetType: Self
      selfCharacterId: warrior
      itemId: Assets/Tactics/Battle/Buffs/Frozen.asset
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Heal
      targetType: Self
      selfCharacterId: warrior
      amount: 4
assertions:
  - kind: inventoryContains
    adapter: Map
    expected: potion_healing
    parameters: {}
  - kind: rosterCharacterHasPendingBuff
    adapter: Map
    target: warrior
    expected: Frozen
    parameters: {}
  - kind: rosterCharacterPendingBuffHasIcon
    adapter: Map
    target: warrior
    expected: Frozen
    parameters: {}
  - kind: rosterCharacterHpEquals
    adapter: Map
    target: warrior
    expected: 10
    parameters: {}
timeoutMs: 10000
---

# Map - EventResultPersistenceWriteback

验证事件结果经统一结果层应用后，`inventory / pending buff / hp` 都能从保存状态中重新读到。

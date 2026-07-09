---
feature: Map
scenario: EventResultSelfBuff
tags:
  - map
  - event
  - unified-result
  - buff
  - self
requiredAdapters:
  - Map
setup: []
actions:
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Buff
      targetType: Self
      selfCharacterId: warrior
      itemId: Assets/Tactics/ScriptableObjects/Buffs/CurseDamageAmplifier.asset
assertions:
  - kind: rosterCharacterHasPendingBuff
    adapter: Map
    target: warrior
    expected: CurseDamageAmplifier
    parameters: {}
timeoutMs: 10000
---

# Map - EventResultSelfBuff

直接验证 `EventResult.Buff(Self)` 会把待生效 Buff 写到指定角色。

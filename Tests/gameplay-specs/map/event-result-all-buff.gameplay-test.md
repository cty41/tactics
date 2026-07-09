---
feature: Map
scenario: EventResultAllBuff
tags:
  - map
  - event
  - unified-result
  - buff
  - all
requiredAdapters:
  - Map
setup: []
actions:
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Buff
      targetType: All
      itemId: Assets/Tactics/ScriptableObjects/Buffs/CurseDamageAmplifier.asset
assertions:
  - kind: runtimeRosterCharacterHasPendingBuff
    adapter: Map
    target: warrior
    expected: CurseDamageAmplifier
    parameters: {}
  - kind: runtimeRosterCharacterHasPendingBuff
    adapter: Map
    target: mage
    expected: CurseDamageAmplifier
    parameters: {}
timeoutMs: 10000
---

# Map - EventResultAllBuff

直接验证 `EventResult.Buff(All)` 会把统一结果层中的 Buff 广播到整队。

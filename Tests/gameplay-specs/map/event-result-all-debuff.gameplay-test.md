---
feature: Map
scenario: EventResultAllDebuff
tags:
  - map
  - event
  - unified-result
  - debuff
  - all
requiredAdapters:
  - Map
setup: []
actions:
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Debuff
      targetType: All
      itemId: Assets/Tactics/ScriptableObjects/Buffs/CurseDamageAmplifier.asset
assertions:
  - kind: rosterCharacterHasPendingBuff
    adapter: Map
    target: warrior
    expected: CurseDamageAmplifier
    parameters: {}
  - kind: rosterCharacterHasPendingBuff
    adapter: Map
    target: mage
    expected: CurseDamageAmplifier
    parameters: {}
timeoutMs: 10000
---

# Map - EventResultAllDebuff

直接验证 `EventResult.Debuff(All)` 也会经统一结果层写入待生效状态。

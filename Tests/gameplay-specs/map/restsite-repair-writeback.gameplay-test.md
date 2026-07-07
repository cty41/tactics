---
feature: Map
scenario: RestSiteRepairWriteback
tags:
  - map
  - restsite
  - writeback
requiredAdapters:
  - Map
setup:
  - kind: setRosterCharacterState
    parameters:
      characterId: warrior
      currentHp: 5
      currentMp: 3
      isDead: false
actions:
  - kind: applyRestSiteResult
    adapter: Map
    parameters:
      healPercent: 0.3
      manaHealPercent: 0.3
assertions:
  - kind: rosterCharacterHpEquals
    adapter: Map
    target: warrior
    expected: 12
    parameters: {}
  - kind: rosterCharacterMpEquals
    adapter: Map
    target: warrior
    expected: 8
    parameters: {}
timeoutMs: 10000
---

# Map - RestSiteRepairWriteback

最小补给回归：补给点通过统一结果结构恢复 HP/MP，并写回冒险状态。

---
feature: Map
scenario: RestSiteSkipsDead
tags:
  - map
  - restsite
  - death
requiredAdapters:
  - Map
setup:
  - kind: setRosterCharacterState
    adapter: Map
    parameters:
      characterId: warrior
      currentHp: 0
      currentMp: 0
      isDead: true
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
    expected: 0
    parameters: {}
  - kind: rosterCharacterMpEquals
    adapter: Map
    target: warrior
    expected: 0
    parameters: {}
  - kind: rosterCharacterDeadEquals
    adapter: Map
    target: warrior
    expected: true
    parameters: {}
timeoutMs: 10000
---

# Map - RestSiteSkipsDead

补给点边界回归：死亡角色不会被休息站恢复或复活。

---
feature: Map
scenario: PureRunEventPartyDefeat
tags: [pure-run, mystery, defeat, summary]
requiredAdapters: [Map]
setup:
  - kind: setRunSeed
    parameters: { seed: 9102 }
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
  - kind: setRosterCharacterState
    parameters: { characterId: pure_run_mage, currentHp: 1, isDead: false }
  - kind: setRosterCharacterState
    parameters: { characterId: pure_run_necromancer, currentHp: 1, isDead: false }
  - kind: setRosterCharacterState
    parameters: { characterId: pure_run_amazon, currentHp: 1, isDead: false }
actions:
  - kind: applyEventResult
    adapter: Map
    parameters:
      resultType: Damage
      targetType: All
      partyCharacterIds: [pure_run_mage, pure_run_necromancer, pure_run_amazon]
      amount: 99
  - kind: commitEventPartyDefeat
    adapter: Map
    parameters: {}
assertions:
  - kind: completedSummaryOutcomeEquals
    adapter: Map
    expected: Defeat
    parameters: {}
  - kind: mapIsActive
    adapter: Map
    expected: false
    parameters: {}
timeoutMs: 10000
---

# Map - Pure Run Event Party Defeat

Mystery 伤害通过正式事件结果链击杀全队，并进入与战斗失败相同的 Defeat 快照入口。

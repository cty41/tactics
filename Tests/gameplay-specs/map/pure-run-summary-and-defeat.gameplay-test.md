---
feature: Map
scenario: PureRunSummaryAndDefeat
tags:
  - pure-run
  - summary
  - defeat
requiredAdapters:
  - Map
setup:
  - kind: setRunSeed
    parameters:
      seed: 811
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
actions:
  - kind: exercisePureRunSummaryAndDefeat
    parameters:
      rewardGold: 20
      spendGold: 5
      itemId: life_potion
assertions:
  - kind: mapIsActive
    expected: false
    parameters: {}
  - kind: completedSummaryGoldEquals
    expected: 20
    parameters: {}
  - kind: completedSummaryContainsItem
    expected: life_potion
    parameters: {}
  - kind: completedSummaryOutcomeEquals
    expected: Defeat
    parameters: {}
  - kind: completedSummaryNodesVisitedEquals
    expected: 1
    parameters: {}
  - kind: completedSummaryEventsCompletedEquals
    expected: 1
    parameters: {}
timeoutMs: 10000
---

# Map - PureRunSummaryAndDefeat

验证累计统计按稳定键去重、消耗与花费不回退总获得量，以及战败终局快照在清理活跃局后仍可读取。

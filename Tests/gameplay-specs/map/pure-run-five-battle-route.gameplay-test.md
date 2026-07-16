---
feature: Map
scenario: PureRunFiveBattleRoute
tags:
  - map
  - pure-run
  - vertical-slice
  - integration
requiredAdapters:
  - Map
setup:
  - kind: setRunSeed
    parameters:
      seed: 20260715
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
actions:
  - kind: completeNode
    parameters:
      nodeId: layer_01_battle
  - kind: completeNode
    parameters:
      nodeId: layer_02_battle
  - kind: completeNode
    parameters:
      nodeId: layer_03_battle
  - kind: completeNode
    parameters:
      nodeId: layer_04_rest
  - kind: completeNode
    parameters:
      nodeId: layer_05_battle
  - kind: completeNode
    parameters:
      nodeId: layer_06_event
  - kind: completeNode
    parameters:
      nodeId: layer_07_special
assertions:
  - kind: mapIsActive
    expected: true
    parameters: {}
  - kind: currentNodeEquals
    expected: layer_07_special
    parameters: {}
  - kind: visitedNodeCountEquals
    expected: 8
    parameters: {}
  - kind: battleVictoryCountEquals
    expected: 5
    parameters: {}
  - kind: consumableCountEquals
    expected: 0
    parameters: {}
timeoutMs: 20000
---

# Map - Pure Run Five Battle Route

固定 Run 种子，沿两次服务/事件分支完成最短路线，验证整局恰好包含五场战斗且起始不携带消耗品。

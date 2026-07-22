---
feature: Map
scenario: MysteryDeterminismAndReentry
tags:
  - pure-run
  - mystery
  - transaction
requiredAdapters:
  - Map
setup:
  - kind: setRunSeed
    parameters:
      seed: 7301
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
actions:
  - kind: resolveNodeEventOption
    adapter: Map
    parameters:
      nodeId: layer_04_event
      optionId: disarm_trap
  - kind: reloadPureRunSession
    adapter: Map
    parameters: {}
  - kind: resolveNodeEventOption
    adapter: Map
    parameters:
      nodeId: layer_04_event
      optionId: disarm_trap
assertions:
  - kind: mysteryEventIdsUnique
    adapter: Map
    expected: true
    parameters: {}
  - kind: nodeEventIdEquals
    adapter: Map
    target: layer_04_event
    expected: cursed_chest_001
    parameters: {}
  - kind: nodeEventIdEquals
    adapter: Map
    target: layer_06_event
    expected: lost_villager_001
    parameters: {}
  - kind: nodeTransactionPhaseEquals
    adapter: Map
    target: layer_04_event
    expected: Resolved
    parameters: {}
  - kind: nodeTransactionRewardAppliedEquals
    adapter: Map
    target: layer_04_event
    expected: true
    parameters: {}
  - kind: transactionApplicationCountEquals
    adapter: Map
    expected: 1
    parameters:
      key: node:layer_04_event:Mystery
timeoutMs: 10000
---

# Map - MysteryDeterminismAndReentry

固定 seed 为两个 Mystery 分配不同事件；同一选项结算后重载并再次执行不会重抽或重复应用。

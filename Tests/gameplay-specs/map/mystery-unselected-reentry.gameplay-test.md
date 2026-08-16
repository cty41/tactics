---
feature: Map
scenario: MysteryUnselectedReentry
tags: [pure-run, mystery, reentry, interruption]
requiredAdapters: [Map]
setup:
  - kind: setRunSeed
    parameters: { seed: 7301 }
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
actions:
  - kind: beginNodeTransaction
    adapter: Map
    parameters: { nodeId: layer_04_event }
  - kind: reloadPureRunSession
    adapter: Map
    parameters: {}
assertions:
  - kind: nodeTransactionPhaseEquals
    adapter: Map
    target: layer_04_event
    expected: Entered
    parameters: {}
  - kind: nodeTransactionRewardAppliedEquals
    adapter: Map
    target: layer_04_event
    expected: false
    parameters: {}
  - kind: nodeIsConsumed
    adapter: Map
    target: layer_04_event
    expected: false
    parameters: {}
timeoutMs: 10000
---

# Map - Mystery Unselected Reentry

进入 Mystery 后尚未选择选项即中断；重载后保留 Entered 状态，不生成结果、不发奖励且节点未消费。

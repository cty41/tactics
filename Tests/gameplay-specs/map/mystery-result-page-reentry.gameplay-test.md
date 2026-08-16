---
feature: Map
scenario: MysteryResultPageReentry
tags:
  - pure-run
  - mystery
  - reentry
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
      optionId: leave
  - kind: reloadPureRunSession
    adapter: Map
    parameters: {}
assertions:
  - kind: nodeTransactionPhaseEquals
    adapter: Map
    target: layer_04_event
    expected: Resolved
    parameters: {}
  - kind: nodeIsConsumed
    adapter: Map
    target: layer_04_event
    expected: false
    parameters: {}
timeoutMs: 10000
---

# Map - MysteryResultPageReentry

结果页中断并重载时仍恢复 Resolved，节点尚未消费；提交阶段由事务单元测试覆盖。

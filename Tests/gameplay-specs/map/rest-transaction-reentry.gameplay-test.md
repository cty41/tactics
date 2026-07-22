---
feature: Map
scenario: RestTransactionReentry
tags:
  - pure-run
  - rest
  - transaction
requiredAdapters:
  - Map
setup:
  - kind: setRunSeed
    parameters:
      seed: 7302
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
  - kind: setRosterCharacterState
    parameters:
      characterId: pure_run_mage
      currentHp: 1
      currentMp: 1
      isDead: false
actions:
  - kind: applyRestNodeTransaction
    adapter: Map
    parameters:
      nodeId: layer_04_rest
      healPercent: 0.3
      manaHealPercent: 0.3
  - kind: reloadPureRunSession
    adapter: Map
    parameters: {}
  - kind: applyRestNodeTransaction
    adapter: Map
    parameters:
      nodeId: layer_04_rest
      healPercent: 0.3
      manaHealPercent: 0.3
assertions:
  - kind: rosterCharacterHpEquals
    adapter: Map
    target: pure_run_mage
    expected: 8
    parameters: {}
  - kind: rosterCharacterMpEquals
    adapter: Map
    target: pure_run_mage
    expected: 6
    parameters: {}
  - kind: transactionApplicationCountEquals
    adapter: Map
    expected: 1
    parameters:
      key: node:layer_04_rest:RestSite
  - kind: nodeTransactionPhaseEquals
    adapter: Map
    target: layer_04_rest
    expected: Resolved
    parameters: {}
timeoutMs: 10000
---

# Map - RestTransactionReentry

休息效果在重载前后只应用一次，结果阶段保持可恢复且不会提前消费节点。

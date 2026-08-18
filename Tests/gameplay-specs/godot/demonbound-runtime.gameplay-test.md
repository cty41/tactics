---
feature: Demonbound
scenario: RuntimeState
tags: [godot, demonbound, corruption, possession, isolated-save]
requiredAdapters: [Map, PlayerInput, UI, Battle]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: demonbound-ready-v1, path: "validated://demonbound-ready-v1", semanticHash: be97eff7ae17ad478fa596f2baa23ddc9ce9977c39be1da9b46369dc049fe959 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: restartGodotMain
    adapter: UI
    parameters: {}
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: battleReady, maximumFrames: 300 }
assertions:
  - kind: demonboundCorruptionEquals
    adapter: Battle
    target: party-pure_run_demonbound
    expected: 0
    parameters: {}
  - kind: demonboundPossessedEquals
    adapter: Battle
    target: party-pure_run_demonbound
    expected: false
    parameters: {}
  - kind: runtimeHasNoErrors
    adapter: UI
    expected: true
    parameters: {}
  - kind: productionSaveUnchanged
    adapter: Map
    expected: true
    parameters: {}
timeoutMs: 30000
---

# Demonbound runtime state

从隔离 V7 checkpoint 进入正式战斗，验证魔剑士腐化与附身状态已接入 Godot v3 probe，且不污染生产存档。

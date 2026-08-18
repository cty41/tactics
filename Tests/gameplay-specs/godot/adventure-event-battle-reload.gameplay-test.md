---
feature: AdventureBoard
scenario: EventBattleContextSurvivesReload
tags: [godot, adventure-board, isolated-save, validated-checkpoint, reload]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: layer4-event-ready-v1, path: "validated://layer4-event-ready-v1", semanticHash: e87eba856e590e14226d417ed9d61b7b5e7656933597a83dc0fb29fe7ed6fa72 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: layer_04_event
    parameters: { targetKind: MapNode }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: cursed-chest
    parameters: { targetKind: AdventureObject }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: eventBattleReady, maximumFrames: 300 }
  - kind: restartGodotMain
    adapter: UI
    parameters: {}
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: eventBattleReady, maximumFrames: 300 }
assertions:
  - kind: pendingBattleContextKindEquals
    adapter: Map
    expected: CursedChestMimic
    parameters: {}
  - kind: runtimeHasNoErrors
    adapter: UI
    expected: true
    parameters: {}
  - kind: productionSaveUnchanged
    adapter: Map
    expected: true
    parameters: {}
timeoutMs: 60000
---

# Event battle reload

事件战 checkpoint 写入后重启 Main，断言正式战斗与诅咒宝箱上下文均从 V9 存档恢复。

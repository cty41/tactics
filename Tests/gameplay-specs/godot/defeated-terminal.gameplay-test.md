---
feature: GodotPendingAcceptance
scenario: DefeatedTerminal
tags: [godot, defeat, terminal, isolated-save]
requiredAdapters: [Map, PlayerInput, Battle, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: defeat-no-summon-v1, path: "validated://defeat-no-summon-v1", semanticHash: 3da74c45e5d8d8033900cb7f9fb2483928acaca8ea97f6a8886f5249b270a833 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: endTurnOnlyUntilTerminal
    adapter: Battle
    parameters: {}
assertions:
  - kind: terminalSummaryOutcomeEquals
    adapter: Map
    expected: Defeated
    parameters: {}
  - kind: activeRunExistsEquals
    adapter: Map
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

# Defeated terminal through Main

从低生命、无召唤物的合法战前 checkpoint 开始，只提交生产 UI 允许的 End Turn，验证全灭后唯一 Defeated Summary。

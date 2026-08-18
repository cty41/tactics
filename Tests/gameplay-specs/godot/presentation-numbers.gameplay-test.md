---
feature: GodotPendingAcceptance
scenario: PresentationNumbers
tags: [godot, presentation, numbers, isolated-save]
requiredAdapters: [Map, PlayerInput, Battle, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: numbers-mana-v1, path: "validated://numbers-mana-v1", semanticHash: a946a748226f95abbcf3769743570eddccf3afb00b856c4c363e13ab5249bc20 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: endTurnUntilPresentationNumber
    adapter: Battle
    parameters: { kind: Mana, maximumActions: 8 }
  - kind: setPresentationPaused
    adapter: UI
    parameters: { paused: true }
  - kind: setPresentationPaused
    adapter: UI
    parameters: { paused: false }
assertions:
  - kind: presentationNumberEquals
    adapter: UI
    expected: Mana
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

# Committed presentation numbers

通过生产 End Turn 输入产生 MP 恢复；数字事实必须来自 committed events。

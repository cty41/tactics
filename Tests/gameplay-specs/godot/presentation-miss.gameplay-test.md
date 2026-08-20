---
feature: GodotPendingAcceptance
scenario: PresentationMiss
tags: [godot, presentation, numbers, isolated-save]
requiredAdapters: [Map, PlayerInput, Battle, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: numbers-miss-v1, path: "validated://numbers-miss-v1", semanticHash: e15e9251b934399c4c598f99de4cf161fd0589375bfcc2be8e8063d658e9f7cf }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: endTurnUntilPresentationNumber
    adapter: Battle
    parameters: { kind: Miss, maximumActions: 8 }
assertions:
  - kind: presentationNumberEquals
    adapter: UI
    expected: Miss
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

# Committed miss number

Amazon 以 Combat Techniques 作为起始分支且是唯一存活角色；生产 End Turn 输入驱动固定 RNG=2 的敌方攻击，必须产生 committed dodge 和灰色 Miss 数字。

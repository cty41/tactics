---
feature: AdventureBoard
scenario: RestCampfireResolution
tags: [godot, adventure-board, isolated-save, validated-checkpoint]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters:
      id: layer4-choice-ready-v1
      path: validated://layer4-choice-ready-v1
      semanticHash: 26d40a899332e71a34f8cf3016082b6cc899c0071c453f094cc58a527c9156ab
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: mapReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: layer_04_rest
    parameters: { targetKind: MapNode }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: rest-campfire
    parameters: { targetKind: AdventureObject }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: Confirm Rest
    parameters: { observable: uiElement, elementName: Confirm Rest, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Confirm Rest
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: PURE RUN MAP
    parameters: { observable: uiElement, elementName: PURE RUN MAP, maximumFrames: 180 }
assertions:
  - kind: partyResourceSummaryEquals
    adapter: Map
    expected: ["pure_run_mage:12/20:5/15", "pure_run_necromancer:12/20:6/18", "pure_run_amazon:12/20:5/15"]
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

# Rest campfire resolution

从经过哈希验证的 Layer 4 checkpoint 进入正式 Run Map，选择 Rest 节点后进入 Tile 场景，点击火堆打开正式结算界面并确认恢复。

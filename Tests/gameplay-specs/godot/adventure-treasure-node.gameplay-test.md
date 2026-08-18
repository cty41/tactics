---
feature: AdventureBoard
scenario: StandardTreasureChest
tags: [godot, adventure-board, isolated-save, validated-checkpoint]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: layer4-choice-ready-v1, path: "validated://layer4-choice-ready-v1", semanticHash: 26d40a899332e71a34f8cf3016082b6cc899c0071c453f094cc58a527c9156ab }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: mapReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: layer_04_treasure
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
    target: treasure-chest
    parameters: { targetKind: AdventureObject }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: Confirm Treasure
    parameters: { observable: uiElement, elementName: Confirm Treasure, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Confirm Treasure
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: PURE RUN MAP
    parameters: { observable: uiElement, elementName: PURE RUN MAP, maximumFrames: 180 }
assertions:
  - kind: backpackContainsContentId
    adapter: Map
    target: item.equipment.lucky-ring-01
    expected: true
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

# Standard treasure chest

从正式 Layer 4 地图进入 Treasure Tile，点击标准宝箱并确认幂等结算，断言固定装备奖励进入无限背包。

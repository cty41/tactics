---
feature: AdventureBoard
scenario: TwoGroupRouteCommit
tags: [godot, player-input-e2e, adventure-board, isolated-save]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: New Run
    parameters: { observable: uiElement, elementName: New Run, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: New Run
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: pure_run_amazon
    parameters: { targetKind: AdventureActor }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: pure_run_demonbound
    parameters: { targetKind: AdventureActor }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: pure_run_mage
    parameters: { targetKind: AdventureActor }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: start-exit
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: starting_skill__skill_amazon_thrust_lv1
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: starting_skill__skill_mage_fireball_lv1
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 7,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: route-overview
    parameters: { targetKind: AdventureObject }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureSceneChanged, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: route-a-store
    parameters: { targetKind: RouteNode }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: route-b-event
    parameters: { targetKind: RouteNode }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: route-submit
    parameters: { targetKind: AdventureObject }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: routeCommitted, maximumFrames: 180 }
assertions:
  - kind: runNodeLifecycleEquals
    adapter: Map
    expected: RouteCommitted
    parameters: {}
  - kind: routeCandidateNodeIdsEqual
    adapter: Map
    expected: [route-a-store, route-b-event]
    parameters: {}
  - kind: adventureObjectStateEquals
    adapter: Map
    target: route-submit
    expected: Committed
    parameters: {}
  - kind: adventureObjectStateEquals
    adapter: Map
    target: route-depart
    expected: Ready
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

# Two-group route commit

从正式开局进入 Tile 路线总览，依次从 A、B 两组三选一，并通过正式 Submit 交互物锁定路线。断言提交生命周期、两项已选节点和离开入口状态。

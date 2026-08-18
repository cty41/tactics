---
feature: AdventureBoard
scenario: PartialRouteAndCommitSurviveReload
tags: [godot, adventure-board, isolated-save, reload]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: New Run
    parameters: { targetKind: UiElement }
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
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 7,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: route-overview
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: route-a-store
    parameters: { targetKind: RouteNode }
  - kind: restartGodotMain
    adapter: UI
    parameters: {}
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: route-b-event
    parameters: { targetKind: RouteNode }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: route-submit
    parameters: { targetKind: AdventureObject }
  - kind: restartGodotMain
    adapter: UI
    parameters: {}
assertions:
  - kind: runNodeLifecycleEquals
    adapter: Map
    expected: RouteCommitted
    parameters: {}
  - kind: routeCandidateNodeIdsEqual
    adapter: Map
    expected: [route-a-store, route-b-event]
    parameters: {}
  - kind: productionSaveUnchanged
    adapter: Map
    expected: true
    parameters: {}
timeoutMs: 60000
---

# Route reload

分别在第一组路线选择后和最终提交后重启 Main，断言 V9 恢复选择且提交状态不可回退。

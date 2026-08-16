---
feature: PlayerInput
scenario: BattlePlayerInputSmoke
tags: [pure-run, battle, player-input-e2e]
requiredAdapters: [PlayerInput, UI, Battle]
setup:
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: NewGameButton }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: NewGameButton
    parameters: { targetKind: UiElement }
  - kind: waitForFrames
    adapter: PlayerInput
    parameters: { frames: 10 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: SkillOption_0, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: SkillOption_0
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: ConfirmButton, interactable: true, maximumFrames: 120 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ConfirmButton
    parameters: { targetKind: UiElement }
  - kind: waitForFrames
    adapter: PlayerInput
    parameters: { frames: 3 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: SkillOption_0, maximumFrames: 120 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: SkillOption_0
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: ConfirmButton, interactable: true, maximumFrames: 120 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ConfirmButton
    parameters: { targetKind: UiElement }
  - kind: waitForFrames
    adapter: PlayerInput
    parameters: { frames: 3 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: SkillOption_0, maximumFrames: 120 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: SkillOption_0
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: ConfirmButton, interactable: true, maximumFrames: 120 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ConfirmButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: mapReady, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Reachable
    parameters: { targetKind: MapNode, nodeType: Battle }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: battleReady, maximumFrames: 600 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: humanTurn, maximumFrames: 3600 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: CurrentPlayer
    parameters: { targetKind: BattleUnit }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: MoveButton
    parameters: { targetKind: UiElement }
  - kind: rightClickPointerTarget
    adapter: PlayerInput
    target: CurrentPlayer
    parameters: { targetKind: BattleUnit }
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: battleEnded, maximumFrames: 300 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: BattleSettlementRoot, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: BattleSettlementRoot
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: ContinueButton, interactable: true, maximumFrames: 300 }
assertions:
  - kind: elementExists
    adapter: UI
    target: ContinueButton
    expected: true
    parameters: {}
timeoutMs: 120000
---

# Battle Through Production Player Input

从 Home 创建真实 Pure Run，点击当前可达的小型战斗节点。玩家单位选择、取消、技能、目标、移动与结束行动只能由虚拟鼠标或键盘经过生产输入链发生，直到自然战斗结算出现。

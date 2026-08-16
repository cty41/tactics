---
feature: PlayerInput
scenario: PureRunMysteryRealPlayerCommit
tags: [pure-run, mystery, commit, player-input-e2e]
requiredAdapters: [PlayerInput, UI, Map, Battle]
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
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: BattleSettlementRoot, maximumFrames: 600 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: BattleSettlementRoot
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: ContinueButton, interactable: true, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ContinueButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: AttributeAllocationRoot, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: AttrPlus_Strength
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ConfirmButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: SkillOption_0, maximumFrames: 120 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: SkillOption_0
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ConfirmButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: mapReady, maximumFrames: 600 }

  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Reachable
    parameters: { targetKind: MapNode, nodeType: Battle }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: battleReady, maximumFrames: 600 }
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: BattleSettlementRoot, maximumFrames: 600 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: BattleSettlementRoot
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: ContinueButton, interactable: true, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ContinueButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: AttributeAllocationRoot, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: AttrPlus_Strength
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ConfirmButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: SkillOption_0, maximumFrames: 120 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: SkillOption_0
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ConfirmButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: mapReady, maximumFrames: 600 }

  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Reachable
    parameters: { targetKind: MapNode, nodeType: Battle }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: battleReady, maximumFrames: 600 }
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: BattleSettlementRoot, maximumFrames: 600 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: BattleSettlementRoot
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: ContinueButton, interactable: true, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ContinueButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: AttributeAllocationRoot, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: AttrPlus_Strength
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ConfirmButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: SkillOption_0, maximumFrames: 120 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: SkillOption_0
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ConfirmButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: mapReady, maximumFrames: 600 }

  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Reachable
    parameters: { targetKind: MapNode, nodeType: Mystery }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: EventOption_0, maximumFrames: 600 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: EventOption_0
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: ResultText, maximumFrames: 300 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: ContinueButton, interactable: true, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ContinueButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: mapReady, maximumFrames: 300 }
assertions:
  - kind: nodeIsConsumed
    adapter: Map
    target: layer_04_event
    expected: true
    parameters: {}
  - kind: nodeTransactionPhaseEquals
    adapter: Map
    target: layer_04_event
    expected: Committed
    parameters: {}
  - kind: currentNodeEquals
    adapter: Map
    expected: layer_04_event
    parameters: {}
  - kind: visitedNodeCountEquals
    adapter: Map
    expected: 5
    parameters: {}
  - kind: battleVictoryCountEquals
    adapter: Map
    expected: 3
    parameters: {}
  - kind: mapIsActive
    adapter: Map
    expected: true
    parameters: {}
timeoutMs: 180000
---

# Pure Run Mystery Real Player Commit

从 Home 开始，经三次初始技能选择、三场真实输入战斗和升级流程到达第四层，再真实点击 Mystery、事件选项与结果页“继续”按钮提交事务并回到地图。断言 layer_04_event 已消耗、事务阶段为 Committed、当前节点停留在事件节点，且三场战斗与五段访问路径完整保留。中断/重进后的会话恢复由 mystery-result-page-reentry 在存档层覆盖；本 spec 的 runtime action 全部通过 PlayerInput，不使用 Map/UI 业务捷径。

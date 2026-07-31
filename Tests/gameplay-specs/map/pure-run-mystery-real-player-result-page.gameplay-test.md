---
feature: PlayerInput
scenario: PureRunMysteryRealPlayerResultPage
tags: [pure-run, mystery, result-page, player-input-e2e]
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
assertions:
  - kind: elementExists
    adapter: UI
    target: ResultText
    expected: true
    parameters: {}
  - kind: elementVisible
    adapter: UI
    target: ResultText
    expected: true
    parameters: {}
  - kind: elementVisible
    adapter: UI
    target: ContinueButton
    expected: true
    parameters: {}
  - kind: elementClassContainsAny
    adapter: UI
    target: ResultText
    expected: [result-success, result-failure]
    parameters: {}
  - kind: nodeTransactionPhaseEquals
    adapter: Map
    target: layer_04_event
    expected: Resolved
    parameters: {}
  - kind: nodeIsConsumed
    adapter: Map
    target: layer_04_event
    expected: false
    parameters: {}
  - kind: mapIsActive
    adapter: Map
    expected: true
    parameters: {}
timeoutMs: 180000
---

# Pure Run Mystery Real Player Result Page

从 Home 经三次初始技能选择、三场真实输入战斗和升级流程到达第四层，再真实点击首个可达 Mystery 节点（layer_04_event）并点击首个事件选项。spec 在结果页停住，断言结果文本存在且带成功/失败样式类（种子随机，二者任一即为通过），事务处于 Resolved 未提交状态。路线不依赖固定事件 ID 与固定判定结果；runtime action 全部通过 PlayerInput，不允许 Map/UI adapter 直接推进、解析或提交事务。

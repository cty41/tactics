---
feature: PlayerInput
scenario: PureRunPlayerInputRoute
tags: [pure-run, journey, reentry, player-input-e2e]
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
    parameters: { observable: uiElement, elementName: BattleSettlementRoot, maximumFrames: 300 }
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
    target: InventoryButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: CharacterNameLabel }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: CloseButton
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
    parameters: { observable: uiElement, elementName: BattleSettlementRoot, maximumFrames: 300 }
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
    parameters: { observable: uiElement, elementName: BattleSettlementRoot, maximumFrames: 300 }
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
    parameters: { targetKind: MapNode, nodeType: Store }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: FirstStoreBuyButton, interactable: true, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: FirstStoreBuyButton
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: CloseButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: mapReady, maximumFrames: 300 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: InventoryButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: StorageSlot_0, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: StorageSlot_0
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: InventoryItemPopover, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: CloseButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: mapReady, maximumFrames: 300 }
assertions:
  - kind: elementExists
    adapter: UI
    target: InventoryButton
    expected: true
    parameters: {}
  - kind: mapIsActive
    adapter: Map
    expected: true
    parameters: {}
  - kind: visitedNodeCountEquals
    adapter: Map
    expected: 5
    parameters: {}
  - kind: battleVictoryCountEquals
    adapter: Map
    expected: 3
    parameters: {}
  - kind: rosterCharacterLevelEquals
    adapter: Map
    target: pure_run_mage
    expected: 2
    parameters: {}
  - kind: rosterCharacterLevelEquals
    adapter: Map
    target: pure_run_necromancer
    expected: 2
    parameters: {}
  - kind: rosterCharacterLevelEquals
    adapter: Map
    target: pure_run_amazon
    expected: 2
    parameters: {}
timeoutMs: 360000
---

# Pure Run Player Input Journey

从 Home 通过正式输入完成三场动态可达战斗、三次结算与显式升级，进入首个可达商店购买物品，并在多次战斗场景重入后从 Inventory 打开该物品详情。路线不依赖固定节点 ID，不允许适配器直接执行技能、授予等级、生成敌人或提交结算。

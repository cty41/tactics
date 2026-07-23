---
feature: PlayerInput
scenario: InventoryReentryPlayerInput
tags: [ui, pure-run, lifecycle, reentry, player-input-e2e]
requiredAdapters: [PlayerInput, UI]
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
    target: InventoryButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: CharacterNameLabel }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: InventoryFilterEquipment
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: CloseButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiHidden, uiId: Inventory }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: InventoryButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: CharacterNameLabel }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: InventoryFilterConsumable
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: CloseButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiHidden, uiId: Inventory }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: InventoryButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: uiElement, elementName: CharacterNameLabel }
assertions:
  - kind: elementText
    adapter: UI
    target: CharacterNameLabel
    expected: Mage
    parameters: {}
  - kind: elementText
    adapter: UI
    target: LevelLabel
    expected: Lv.1
    parameters: {}
  - kind: elementExists
    adapter: UI
    target: CloseButton
    expected: true
    parameters: {}
timeoutMs: 30000
---

# Inventory Reentry Through Player Input

从 Home 通过虚拟鼠标走生产输入链创建 Pure Run，并在同一个 RoguelikeMap 会话中三次打开 Inventory。两次关闭后都必须恢复地图按钮交互，第三次打开时角色信息与关闭按钮仍有效。

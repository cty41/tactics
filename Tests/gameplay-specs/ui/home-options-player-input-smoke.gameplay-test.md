---
feature: PlayerInput
scenario: HomeOptionsPlayerInputSmoke
tags: [ui, home, options, player-input-e2e, smoke]
requiredAdapters: [PlayerInput, UI]
setup:
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: OptionsButton
    parameters: { observable: uiElement, elementName: OptionsButton, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: OptionsButton
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: OptionsRoot
    parameters: { observable: uiElement, elementName: OptionsRoot, maximumFrames: 180 }
assertions:
  - kind: elementExists
    adapter: UI
    target: OptionsRoot
    expected: true
    parameters: {}
  - kind: elementVisible
    adapter: UI
    target: OptionsRoot
    expected: true
    parameters: {}
timeoutMs: 10000
---

# Home Options Player Input Smoke

从 Home 场景等待选项按钮可输入，通过虚拟鼠标沿生产 PlayerInput 链点击 OptionsButton，并验证 OptionsRoot 已创建且可见。该 smoke 使用独立 fixture，避免与较长的 PlayerInput 旅程测试共享残留状态。

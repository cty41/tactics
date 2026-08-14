---
feature: GodotGameplayRunner
scenario: RunnerHomeQuit
tags: [godot, player-input-e2e, isolated-save, smoke]
requiredAdapters: [PlayerInput, UI]
setup:
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: Quit
    parameters: { observable: uiElement, elementName: Quit, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Quit
    parameters: { targetKind: UiElement }
assertions:
  - kind: runtimeHasNoErrors
    adapter: UI
    expected: true
    parameters: {}
timeoutMs: 30000
---

# Godot Gameplay Runner Home Quit Smoke

在隔离 Save Store 中加载正式 `Main.tscn`，等待 Home 的 Quit 按钮，通过 Viewport 虚拟鼠标走生产 GUI 输入链触发退出，并由测试上下文拦截真实进程退出。

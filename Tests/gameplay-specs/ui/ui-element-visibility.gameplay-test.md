---
feature: UI
scenario: UiElementVisibility
tags:
  - ui
  - visibility
requiredAdapters:
  - UI
setup: []
actions:
  - kind: openUI
    parameters:
      uiId: Home
  - kind: clickElement
    parameters:
      elementName: StartButton
assertions:
  - kind: elementVisible
    target: StartButton
    expected: true
    parameters: {}
  - kind: elementEnabled
    target: StartButton
    expected: true
    parameters: {}
timeoutMs: 10000
---

# UI - UiElementVisibility

最小 UI adapter 回归：打开 Home UI，点击开始按钮，验证按钮可见且可用。

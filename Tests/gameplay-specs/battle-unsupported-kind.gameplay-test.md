---
feature: Battle
scenario: BattleUnsupportedKind
tags:
  - battle
requiredAdapters:
  - Battle
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: unsupportedBattleAction
    parameters: {}
assertions:
  - kind: battleIsActive
    expected: true
    parameters: {}
timeoutMs: 10000
---

# Battle - BattleUnsupportedKind

负向样例：校验非法 Battle action kind 被 TS 层拒绝。

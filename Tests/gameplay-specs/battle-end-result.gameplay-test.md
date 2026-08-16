---
feature: Battle
scenario: BattleEndsWithResult
tags:
  - battle
requiredAdapters:
  - Battle
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: advanceTurn
    parameters: {}
  - kind: endBattleWithResult
    parameters: {}
assertions:
  - kind: battleIsActive
    expected: false
    parameters: {}
timeoutMs: 10000
---

# Battle - BattleEndsWithResult

最小 Battle adapter 回归：推进回合后显式结束战斗，并断言战斗不再激活。

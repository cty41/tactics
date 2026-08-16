---
feature: Battle
scenario: BattleAdvancesRound
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
  - kind: advanceTurn
    parameters: {}
assertions:
  - kind: battleIsActive
    expected: true
    parameters: {}
  - kind: currentRoundEquals
    expected: 2
    parameters: {}
timeoutMs: 10000
---

# Battle - BattleAdvancesRound

最小 Battle adapter 回归：推进回合并断言战斗状态与 round。

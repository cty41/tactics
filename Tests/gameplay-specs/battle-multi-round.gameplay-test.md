---
feature: Battle
scenario: BattleMultiRound
tags:
  - battle
  - round
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
  - kind: advanceTurn
    parameters: {}
  - kind: advanceTurn
    parameters: {}
  - kind: advanceTurn
    parameters: {}
assertions:
  - kind: battleIsActive
    expected: true
    parameters: {}
  - kind: currentRoundEquals
    expected: 3
    parameters: {}
  - kind: unitCountEquals
    expected: 2
    parameters:
      playerNumber: 0
  - kind: unitCountEquals
    expected: 2
    parameters:
      playerNumber: 1
timeoutMs: 15000
---

# Battle - BattleMultiRound

多回合 Battle 回归：推进 5 个回合（3 轮完整交替），验证战斗状态、回合数和单位存活数。

---
feature: Battle
scenario: BattleEnterOrBind
tags:
  - battle
  - smoke
requiredAdapters:
  - Battle
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: advanceTurn
    parameters: {}
assertions:
  - kind: battleIsActive
    expected: true
    parameters: {}
  - kind: currentRoundEquals
    expected: 1
    parameters: {}
  - kind: unitCountEquals
    expected: 2
    parameters:
      playerNumber: 0
  - kind: unitCountEquals
    expected: 2
    parameters:
      playerNumber: 1
timeoutMs: 10000
---

# Battle - BattleEnterOrBind

最小 Battle adapter 回归：绑定 BattleController，验证战斗激活状态、回合数和双方单位数。

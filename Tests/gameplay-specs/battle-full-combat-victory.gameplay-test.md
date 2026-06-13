---
feature: Battle
scenario: BattleFullCombatVictory
tags:
  - battle
  - combat
  - integration
requiredAdapters:
  - Battle
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: executeAbility
    parameters:
      commandType: attack
      attackerAlias: p1_0
      targetAlias: p2_0
      damage: 100
assertions:
  - kind: unitHealthEquals
    target: p2_0
    expected: 0
    parameters: {}
  - kind: unitAliveEquals
    target: p2_0
    expected: false
    parameters: {}
  - kind: battleResultEquals
    parameters:
      winnerPlayerNumber: 1
  - kind: battleIsActive
    expected: false
    parameters: {}
timeoutMs: 10000
---

# Battle - BattleFullCombatVictory

完整战斗测试流程：P1 攻击 P2 → P2 死亡 → P1 获胜。

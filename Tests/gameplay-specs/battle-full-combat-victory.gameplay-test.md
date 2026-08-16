---
feature: Battle
scenario: BattleFullCombatVictory
tags:
  - battle
  - combat
  - integration
requiredAdapters:
  - Battle
  - Skill
setup:
  - kind: bindBattleController
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: lethalAttackGraph
      graphKind: singleTargetDamage
      baseDamage: 999
      isRanged: false
      minRange: 1
      maxRange: 1
actions:
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters:
      graphAlias: lethalAttackGraph
      casterAlias: p1_0
      targetAlias: p2_0
assertions:
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
timeoutMs: 15000
---

# Battle - BattleFullCombatVictory

完整战斗测试流程：P1 使用技能图攻击 P2 → P2 死亡 → P1 获胜。

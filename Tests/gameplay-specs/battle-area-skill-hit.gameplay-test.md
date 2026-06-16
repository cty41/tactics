---
feature: Battle
scenario: BattleAreaSkillHit
tags:
  - battle
  - skill
  - aoe
requiredAdapters:
  - Battle
  - Skill
setup:
  - kind: bindBattleController
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: fireballGraph
      graphKind: areaDamage
      baseDamage: 30
      isRanged: true
      minRange: 1
      maxRange: 3
      aoeRadius: 1
actions:
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters:
      graphAlias: fireballGraph
      casterAlias: p1_0
      targetPointAlias: cell_1_0
assertions:
  - kind: unitHealthEquals
    target: p1_0
    expected: 100
    parameters: {}
  - kind: battleIsActive
    expected: true
    parameters: {}
timeoutMs: 15000
---

# Battle - BattleAreaSkillHit

范围技能回归：P1 使用火球术攻击指定格子，验证施法者 HP 不变、战斗仍激活。

---
feature: Battle
scenario: BattleWithRealSkillGraph
tags:
  - battle
  - assets
  - integration
contractIds:
  - SKILL-SHARED-LEGALITY-001
requiredAdapters:
  - Battle
  - Skill
setup:
  - kind: useRealAssets
    parameters: {}
  - kind: loadSkillGraphAsset
    parameters:
      alias: meleeAttackGraph
      assetPath: Assets/Tactics/Battle/Abilities/SkillGraphs/MeleeAttack_Graph.asset
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters:
      graphAlias: meleeAttackGraph
      casterAlias: p1_0
      targetAlias: p2_0
assertions:
  - kind: unitHealthEquals
    target: p2_0
    expected: 19
    parameters: {}
  - kind: battleIsActive
    expected: true
    parameters: {}
timeoutMs: 20000
---

# Battle - BattleWithRealSkillGraph

真实资产 Battle 回归：使用当前权威的 MeleeAttack SkillGraphAsset 执行战斗技能，验证单位 HP 变化。

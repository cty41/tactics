---
feature: Battle
scenario: BattleWithRealFireball
tags:
  - battle
  - assets
  - aoe
requiredAdapters:
  - Battle
  - Skill
setup:
  - kind: useRealAssets
    parameters: {}
  - kind: loadSkillGraphAsset
    parameters:
      alias: fireballGraph
      assetPath: Assets/Tactics/Battle/Abilities/SkillGraphs/Fireball_Lv1_Graph.asset
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters:
      graphAlias: fireballGraph
      casterAlias: p1_0
      targetPointAlias: cell_1_0
assertions:
  - kind: battleIsActive
    expected: true
    parameters: {}
timeoutMs: 20000
---

# Battle - BattleWithRealFireball

真实资产 Battle 回归：使用当前权威的 Fireball Lv1 SkillGraphAsset 执行技能，验证战斗状态。

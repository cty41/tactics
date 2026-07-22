---
feature: Map
scenario: PureRunNaturalBattleDefeat
tags: [pure-run, battle, defeat, summary]
requiredAdapters: [Map, Battle, Skill]
setup:
  - kind: setRunSeed
    parameters: { seed: 9101 }
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
  - kind: bindBattleController
    adapter: Battle
    parameters: {}
  - kind: createSkillGraph
    adapter: Skill
    parameters:
      alias: enemyFinisher
      graphKind: singleTargetDamage
      baseDamage: 999
      isRanged: false
      minRange: 1
      maxRange: 1
actions:
  - kind: beginBattleNode
    adapter: Map
    parameters: { nodeId: layer_01_battle }
  - kind: bindBattleController
    adapter: Battle
    parameters: {}
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters: { graphAlias: enemyFinisher, casterAlias: p2_0, targetAlias: p1_0 }
  - kind: waitForBattleEnd
    adapter: Battle
    parameters: { maxFrames: 120 }
  - kind: commitNaturalBattleDefeat
    adapter: Map
    parameters: { playerNumber: 1 }
assertions:
  - kind: battleDefeatRewardsAreZero
    adapter: Map
    expected: true
    parameters: {}
  - kind: completedSummaryOutcomeEquals
    adapter: Map
    expected: Defeat
    parameters: {}
  - kind: mapIsActive
    adapter: Map
    expected: false
    parameters: {}
timeoutMs: 20000
---

# Map - Pure Run Natural Battle Defeat

敌方通过真实伤害击倒最后一名玩家单位，自然产生战败结果；失败结算不给奖励并生成统一 Defeat 快照。

---
feature: Map
scenario: MapBattleNode
tags:
  - map
  - battle
  - integration
requiredAdapters:
  - Map
  - Battle
  - Skill
setup:
  - kind: loadRoguelikeMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultMapConfig.asset
  - kind: bindBattleController
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: attackGraph
      graphKind: singleTargetDamage
      baseDamage: 50
actions:
  - kind: enterNode
    adapter: Map
    parameters:
      nodeId: battle_node_1
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters:
      graphAlias: attackGraph
      casterAlias: p1_0
      targetAlias: p2_0
  - kind: completeNode
    adapter: Map
    parameters:
      nodeId: battle_node_1
assertions:
  - kind: mapIsActive
    expected: true
    parameters: {}
  - kind: battleIsActive
    expected: true
    parameters: {}
  - kind: nodeIsVisited
    target: battle_node_1
    expected: true
    parameters: {}
  - kind: visitedNodeCountEquals
    expected: 1
    parameters: {}
timeoutMs: 20000
---

# Map - MapBattleNode

Map + Battle 集成回归：加载地图，进入战斗节点，执行战斗技能，完成节点，验证地图和战斗状态。

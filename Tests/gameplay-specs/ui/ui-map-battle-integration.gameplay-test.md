---
feature: UI
scenario: UiMapBattleIntegration
tags:
  - ui
  - map
  - battle
  - integration
requiredAdapters:
  - UI
  - Map
  - Battle
  - Skill
setup:
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
  - kind: bindBattleController
    parameters: {}
  - kind: createSkillGraph
    parameters:
      alias: attackGraph
      graphKind: singleTargetDamage
      baseDamage: 1
actions:
  - kind: openUI
    adapter: UI
    parameters:
      uiId: RoguelikeMap
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters:
      graphAlias: attackGraph
      casterAlias: p1_0
      targetAlias: p2_0
  - kind: completeNode
    adapter: Map
    parameters:
      nodeId: layer_01_battle
assertions:
  - kind: mapIsActive
    expected: true
    parameters: {}
  - kind: battleIsActive
    expected: true
    parameters: {}
  - kind: nodeIsVisited
    target: layer_01_battle
    expected: true
    parameters: {}
  - kind: elementVisible
    target: RootContainer
    expected: true
    parameters: {}
timeoutMs: 25000
---

# UI - UiMapBattleIntegration

UI + Map + Battle 三 adapter 集成回归：打开地图 UI，进入战斗节点，执行战斗技能，完成节点，验证地图、战斗和 UI 状态。

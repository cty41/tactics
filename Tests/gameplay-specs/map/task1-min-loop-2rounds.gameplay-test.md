---
feature: Map
scenario: Task1MinLoop2Rounds
tags:
  - map
  - task1
  - min-loop
  - integration
requiredAdapters:
  - Map
  - Battle
setup:
  - kind: loadRoguelikeMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - kind: bindBattleController
    parameters: {}
  - kind: setRosterCharacterState
    adapter: Map
    parameters:
      characterId: warrior
      currentHp: 5
      currentMp: 3
      isDead: false
actions:
  - kind: enterNode
    adapter: Map
    parameters:
      nodeId: battle_node_1
  - kind: setUnitState
    adapter: Battle
    parameters:
      unitAlias: p1_0
      characterId: warrior
      playerNumber: 0
      health: 9
      mana: 6
  - kind: endBattleWithResult
    adapter: Battle
    parameters:
      winnerPlayerNumber: 1
  - kind: completeNode
    adapter: Map
    parameters:
      nodeId: battle_node_1
  - kind: applyRestSiteResult
    adapter: Map
    parameters:
      healPercent: 0.3
      manaHealPercent: 0.3
  - kind: setAdventureGold
    adapter: Map
    parameters:
      amount: 10
  - kind: buyShopEquipment
    adapter: Map
    parameters:
      equipmentId: sword_01
      price: 4
assertions:
  - kind: nodeIsVisited
    adapter: Map
    target: battle_node_1
    expected: true
    parameters: {}
  - kind: visitedNodeCountEquals
    adapter: Map
    expected: 1
    parameters: {}
  - kind: rosterCharacterHpEquals
    adapter: Map
    target: warrior
    expected: 20
    parameters: {}
  - kind: rosterCharacterMpEquals
    adapter: Map
    target: warrior
    expected: 15
    parameters: {}
  - kind: rosterCharacterDeadEquals
    adapter: Map
    target: warrior
    expected: false
    parameters: {}
  - kind: runGoldEquals
    adapter: Map
    expected: 6
    parameters: {}
  - kind: inventoryContains
    adapter: Map
    expected: sword_01
    parameters: {}
timeoutMs: 25000
---

# Map - Task1MinLoop2Rounds

Task 1 最小闭环回归：战斗结果写回 -> 节点完成 -> 补给恢复 -> Gold 兑换 -> 再次持有可继续选择的 run state。

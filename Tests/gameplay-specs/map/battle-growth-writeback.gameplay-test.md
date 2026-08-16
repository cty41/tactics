---
feature: Map
scenario: BattleGrowthWriteback
tags:
  - map
  - battle
  - growth
  - writeback
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
      level: 1
      experience: 0
      attributePoints: 0
      currentHp: 7
      currentMp: 4
      isDead: false
actions:
  - kind: setUnitState
    adapter: Battle
    parameters:
      unitAlias: p1_0
      characterId: warrior
      playerNumber: 1
      health: 9
      mana: 6
  - kind: endBattleWithResult
    adapter: Battle
    parameters:
      winnerPlayerNumber: 1
      skipControllerEndBattle: true
      applyRoguelikeWriteback: true
assertions:
  - kind: rosterCharacterExperienceEquals
    adapter: Map
    target: warrior
    expected: 30
    parameters: {}
timeoutMs: 20000
---

# Map - BattleGrowthWriteback

战后成长结果回归：战斗胜利后，经验值正确写回对应 roster 角色。

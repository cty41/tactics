---
feature: Map
scenario: BattleResultWriteback
tags:
  - map
  - battle
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
actions:
  - kind: setRosterCharacterState
    adapter: Map
    parameters:
      characterId: warrior
      currentHp: 7
      currentMp: 4
      isDead: false
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
assertions:
  - kind: rosterCharacterHpEquals
    adapter: Map
    target: warrior
    expected: 19
    parameters: {}
  - kind: rosterCharacterMpEquals
    adapter: Map
    target: warrior
    expected: 11
    parameters: {}
  - kind: rosterCharacterDeadEquals
    adapter: Map
    target: warrior
    expected: false
    parameters: {}
timeoutMs: 20000
---

# Map - BattleResultWriteback

最小写回回归：战斗结束后，将战斗层的 HP/MP/死亡态同步回冒险状态。

---
feature: Map
scenario: BattleDeathWriteback
tags:
  - map
  - battle
  - death
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
      currentHp: 7
      currentMp: 4
      isDead: false
actions:
  - kind: setUnitState
    adapter: Battle
    parameters:
      unitAlias: p1_0
      characterId: warrior
      playerNumber: 0
      health: 0
      mana: 0
      isDowned: true
  - kind: endBattleWithResult
    adapter: Battle
    parameters:
      winnerPlayerNumber: 2
assertions:
  - kind: rosterCharacterHpEquals
    adapter: Map
    target: warrior
    expected: 0
    parameters: {}
  - kind: rosterCharacterMpEquals
    adapter: Map
    target: warrior
    expected: 0
    parameters: {}
  - kind: rosterCharacterDeadEquals
    adapter: Map
    target: warrior
    expected: true
    parameters: {}
timeoutMs: 20000
---

# Map - BattleDeathWriteback

战后死亡态回写回归：战斗层角色死亡后，将 HP/MP/死亡态同步回冒险状态。

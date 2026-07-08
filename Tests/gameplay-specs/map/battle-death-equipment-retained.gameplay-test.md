---
feature: Map
scenario: BattleDeathEquipmentRetained
tags:
  - map
  - battle
  - death
  - equipment
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
      equipmentSlot: Weapon
      equipmentId: sword_01
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
  - kind: rosterCharacterDeadEquals
    adapter: Map
    target: warrior
    expected: true
    parameters: {}
  - kind: rosterCharacterEquipmentEquals
    adapter: Map
    target: warrior
    expected: sword_01
    parameters:
      equipmentSlot: Weapon
timeoutMs: 20000
---

# Map - BattleDeathEquipmentRetained

战后装备保留回归：角色死亡后，CharacterDefinition 上的已装备武器仍然保留。

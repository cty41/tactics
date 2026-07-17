---
feature: Consumable
scenario: ManaPotionExplicitAdjacentTarget
tags:
  - battle
  - consumable
  - mana
requiredAdapters:
  - Map
  - Battle
setup:
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: addConsumableInstance
    adapter: Map
    parameters:
      definitionId: mana_potion
      instanceId: mana_use
  - kind: carryConsumableToRosterCharacter
    adapter: Map
    parameters:
      characterId: pure_run_mage
      instanceId: mana_use
  - kind: setUnitState
    adapter: Battle
    parameters:
      unitAlias: p1_0
      characterId: pure_run_mage
      playerNumber: 1
  - kind: setUnitState
    adapter: Battle
    parameters:
      unitAlias: p2_0
      playerNumber: 1
      maxMana: 20
      mana: 2
      isDowned: false
  - kind: moveUnit
    adapter: Battle
    parameters:
      unitAlias: p2_0
      cellAlias: cell_1_0
  - kind: useCarriedConsumable
    adapter: Battle
    parameters:
      casterAlias: p1_0
      targetAlias: p2_0
      characterId: pure_run_mage
assertions:
  - kind: unitManaEquals
    adapter: Battle
    target: p2_0
    expected: 8
    parameters: {}
  - kind: consumableInstanceExists
    adapter: Map
    target: mana_use
    expected: false
    parameters: {}
timeoutMs: 20000
---

# Consumable - ManaPotionExplicitAdjacentTarget

角色明确选择正交相邻友军，恢复 6 点魔法并立即消耗携带实例。

---
feature: Consumable
scenario: CleansingPotionRemovesOnlyHarmful
tags:
  - battle
  - consumable
  - cleanse
  - buff
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
      definitionId: cleansing_potion
      instanceId: cleanse_use
  - kind: carryConsumableToRosterCharacter
    adapter: Map
    parameters:
      characterId: pure_run_mage
      instanceId: cleanse_use
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
      isDowned: false
  - kind: moveUnit
    adapter: Battle
    parameters:
      unitAlias: p2_0
      cellAlias: cell_1_0
  - kind: addBuff
    adapter: Battle
    parameters:
      unitAlias: p2_0
      buffName: Counter
      configPath: Assets/Tactics/Battle/Buffs/Counter.asset
      duration: 3
  - kind: addBuff
    adapter: Battle
    parameters:
      unitAlias: p2_0
      buffName: Frozen
      configPath: Assets/Tactics/Battle/Buffs/Frozen.asset
      duration: 3
  - kind: useCarriedConsumable
    adapter: Battle
    parameters:
      casterAlias: p1_0
      targetAlias: p2_0
      characterId: pure_run_mage
assertions:
  - kind: unitHasBuff
    adapter: Battle
    target: p2_0
    expected: Counter
    parameters: {}
  - kind: unitDoesNotHaveBuff
    adapter: Battle
    target: p2_0
    expected: Frozen
    parameters: {}
  - kind: consumableInstanceExists
    adapter: Map
    target: cleanse_use
    expected: false
    parameters: {}
timeoutMs: 20000
---

# Consumable - CleansingPotionRemovesOnlyHarmful

净化药水移除全部 Harmful Buff，同时保留 Beneficial Buff，并正常消耗。

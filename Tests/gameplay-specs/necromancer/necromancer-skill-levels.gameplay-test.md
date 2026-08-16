---
feature: NecromancerSkillLevels
scenario: SelectedCorpseSummonsExactlyOneLevelTwoSkeleton
tags:
  - battle
  - necromancer
  - summon
  - corpse
  - level-up
requiredAdapters:
  - Skill
  - Battle
setup:
  - kind: useRealAssets
    parameters: {}
  - kind: createSkillTestWorld
    parameters: {}
  - kind: loadSkillGraphAsset
    parameters:
      alias: summonSkeletonLevelTwo
      assetPath: Assets/Tactics/Battle/Abilities/SkillGraphs/SummonSkeleton_Lv2_Graph.asset
  - kind: createCell
    parameters: { alias: casterCell, x: 0, y: 0 }
  - kind: createCell
    parameters: { alias: firstCorpseCell, x: 1, y: 0 }
  - kind: createCell
    parameters: { alias: selectedCorpseCell, x: 2, y: 0 }
  - kind: createUnit
    parameters: { alias: necromancer, playerNumber: 0, cellAlias: casterCell, mana: 20, maxMana: 20 }
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases: [necromancer]
actions:
  - kind: spawnInteractableCorpse
    parameters: { cellAlias: firstCorpseCell }
  - kind: spawnInteractableCorpse
    parameters: { cellAlias: selectedCorpseCell }
  - kind: executeSkillGraph
    parameters:
      graphAlias: summonSkeletonLevelTwo
      casterAlias: necromancer
      targetPointAlias: selectedCorpseCell
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: interactableCorpseExistsAt
    target: firstCorpseCell
    expected: true
    parameters: {}
  - kind: interactableCorpseExistsAt
    target: selectedCorpseCell
    expected: false
    parameters: {}
  - kind: cellOccupiedByInteractable
    target: firstCorpseCell
    expected: true
    parameters: {}
  - kind: cellOccupiedByInteractable
    target: selectedCorpseCell
    expected: false
    parameters: {}
timeoutMs: 10000
---

# Necromancer Skill Levels - Selected Corpse Transaction

使用真实二级召唤骷髅图和真实 Corpse 组件，验证一次释放只消耗玩家选择的尸体，其他尸体保持可用。

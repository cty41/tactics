---
feature: AmazonSkillLevels
scenario: PoisonSpearLevelTwoDropsUniqueSpearAndPoisonsCross
tags:
  - battle
  - amazon
  - spear
  - poison
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
      alias: poisonSpearLevelTwo
      assetPath: Assets/Tactics/Battle/Abilities/SkillGraphs/PoisonSpear_Lv2_Graph.asset
  - kind: createCell
    parameters: { alias: casterCell, x: 0, y: 1 }
  - kind: createCell
    parameters: { alias: targetCell, x: 3, y: 1 }
  - kind: createCell
    parameters: { alias: crossCell, x: 3, y: 2 }
  - kind: createCell
    parameters: { alias: diagonalCell, x: 4, y: 2 }
  - kind: createCell
    parameters: { alias: spearDropCell, x: 4, y: 1 }
  - kind: createCell
    parameters: { alias: connector1, x: 1, y: 1 }
  - kind: createCell
    parameters: { alias: connector2, x: 2, y: 1 }
  - kind: createUnit
    parameters: { alias: amazon, playerNumber: 0, cellAlias: casterCell, health: 40, maxHealth: 40, mana: 20, maxMana: 20, luck: -100 }
  - kind: createUnit
    parameters: { alias: target, playerNumber: 1, cellAlias: targetCell, health: 40, maxHealth: 40, defenceFactor: 0, luck: -100 }
  - kind: createUnit
    parameters: { alias: cross, playerNumber: 1, cellAlias: crossCell, health: 40, maxHealth: 40, defenceFactor: 0, luck: -100 }
  - kind: createUnit
    parameters: { alias: diagonal, playerNumber: 1, cellAlias: diagonalCell, health: 40, maxHealth: 40, defenceFactor: 0, luck: -100 }
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases: [amazon]
actions:
  - kind: executeSkillGraph
    parameters:
      graphAlias: poisonSpearLevelTwo
      casterAlias: amazon
      primaryTargetAlias: target
      targetPointAlias: targetCell
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: unitHealthEquals
    target: target
    expected: 30
    parameters: {}
  - kind: unitHasBuff
    target: target
    expected: Poison
    parameters: {}
  - kind: unitHasBuff
    target: cross
    expected: Poison
    parameters: {}
  - kind: unitBuffCountEquals
    target: diagonal
    expected: 0
    parameters: { buffName: Poison }
  - kind: spearHolderEquals
    expected: none
    parameters: {}
  - kind: spearCellEquals
    expected: spearDropCell
    parameters: {}
timeoutMs: 10000
---

# Amazon Poison Spear Lv2

使用真实二级毒矛图验证主目标直接伤害、十字中毒范围，以及唯一实体长矛的确定性落点。

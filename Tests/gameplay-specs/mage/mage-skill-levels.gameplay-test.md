---
feature: MageSkillLevels
scenario: FireballLevelTwoCrossSplash
tags:
  - battle
  - mage
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
      alias: fireballLevelTwo
      assetPath: Assets/Tactics/Battle/Abilities/SkillGraphs/Fireball_Lv2_Graph.asset
  - kind: createCell
    parameters: { alias: casterCell, x: 0, y: 0 }
  - kind: createCell
    parameters: { alias: lineCell, x: 1, y: 0 }
  - kind: createCell
    parameters: { alias: primaryCell, x: 2, y: 0 }
  - kind: createCell
    parameters: { alias: splashCell, x: 2, y: 1 }
  - kind: createCell
    parameters: { alias: diagonalCell, x: 3, y: 1 }
  - kind: createUnit
    parameters: { alias: mage, playerNumber: 0, cellAlias: casterCell, mana: 20, maxMana: 20 }
  - kind: createUnit
    parameters: { alias: primary, playerNumber: 1, cellAlias: primaryCell, health: 20, maxHealth: 20, defenceFactor: 0 }
  - kind: createUnit
    parameters: { alias: splash, playerNumber: 1, cellAlias: splashCell, health: 20, maxHealth: 20, defenceFactor: 0 }
  - kind: createUnit
    parameters: { alias: diagonal, playerNumber: 1, cellAlias: diagonalCell, health: 20, maxHealth: 20, defenceFactor: 0 }
  - kind: setTurnContext
    parameters:
      currentPlayerNumber: 0
      playableUnitAliases: [mage]
actions:
  - kind: executeSkillGraph
    parameters:
      graphAlias: fireballLevelTwo
      casterAlias: mage
      targetPointAlias: primaryCell
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: unitHealthEquals
    target: primary
    expected: 16
    parameters: {}
  - kind: unitHealthEquals
    target: splash
    expected: 18
    parameters: {}
  - kind: unitHealthEquals
    target: diagonal
    expected: 20
    parameters: {}
timeoutMs: 10000
---

# Mage Skill Levels - Fireball Level Two Cross Splash

加载真实二级火球图，验证主目标增伤、正交溅射以及斜角目标不受影响。

---
feature: SharedBattlePrimitives
scenario: SummonRegistryOrder
tags:
  - battle
  - summon
requiredAdapters:
  - Skill
  - Battle
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createCell
    parameters: { alias: ownerCell, x: 0, y: 0 }
  - kind: createCell
    parameters: { alias: aCell, x: 1, y: 0 }
  - kind: createCell
    parameters: { alias: bCell, x: 2, y: 0 }
  - kind: createCell
    parameters: { alias: cCell, x: 3, y: 0 }
  - kind: createUnit
    parameters: { alias: owner, playerNumber: 0, cellAlias: ownerCell }
  - kind: createUnit
    parameters: { alias: summonA, playerNumber: 0, cellAlias: aCell }
  - kind: createUnit
    parameters: { alias: summonB, playerNumber: 0, cellAlias: bCell }
  - kind: createUnit
    parameters: { alias: summonC, playerNumber: 0, cellAlias: cCell }
actions:
  - kind: registerSummon
    parameters: { ownerAlias: owner, summonAlias: summonA, category: FireDemon, maximumActive: 2 }
  - kind: registerSummon
    parameters: { ownerAlias: owner, summonAlias: summonB, category: FireDemon, maximumActive: 2 }
  - kind: registerSummon
    parameters: { ownerAlias: owner, summonAlias: summonC, category: FireDemon, maximumActive: 2 }
assertions:
  - kind: summonOrderEquals
    target: owner
    expected: [summonB, summonC]
    parameters: { category: FireDemon }
  - kind: summonCategoryEquals
    target: summonC
    expected: FireDemon
    parameters: {}
  - kind: unitAliveEquals
    adapter: Battle
    target: summonA
    expected: false
    parameters: {}
timeoutMs: 10000
---

# Shared Battle Primitives - Summon Registry Order

验证同一召唤者和类别按注册顺序维护上限，并在超限时淘汰最早召唤物。

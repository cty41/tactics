---
feature: Necromancer
scenario: CorpseBlocksMovement
tags:
  - necromancer
  - corpse
  - movement
requiredAdapters:
  - Battle
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: killUnit
    parameters:
      unitAlias: p2_0
assertions:
  - kind: unitIsCorpse
    target: p2_0
    expected: true
    parameters: {}
  - kind: unitAliveEquals
    target: p2_0
    expected: false
    parameters: {}
timeoutMs: 10000
---

# Necromancer - CorpseBlocksMovement

验证敌人死亡后尸体占据格子。
尸体通过 IsTaken=true 阻止其他单位进入（由 MoveComponent.IsCellMovableTo 保证）。

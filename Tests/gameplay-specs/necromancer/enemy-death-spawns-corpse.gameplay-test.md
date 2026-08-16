---
feature: Necromancer
scenario: EnemyDeathSpawnsCorpse
tags:
  - necromancer
  - corpse
  - death
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

# Necromancer - EnemyDeathSpawnsCorpse

验证敌人死亡后标记为尸体（IsCorpse=true）。

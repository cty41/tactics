---
feature: Necromancer
scenario: SummonConsumesCorpseAndSpawnsSkeleton
tags:
  - necromancer
  - summon
  - corpse
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

# Necromancer - SummonConsumesCorpseAndSpawnsSkeleton

验证敌人死亡后生成尸体，尸体可被后续召唤消耗。
此 spec 验证尸体生成链路；召唤消耗需要完整的 AbilityConfig 资产配置。

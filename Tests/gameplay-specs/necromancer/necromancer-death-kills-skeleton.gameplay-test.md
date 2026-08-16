---
feature: Necromancer
scenario: NecromancerDeathKillsSkeleton
tags:
  - necromancer
  - summon
  - skeleton
  - linked-death
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

# Necromancer - NecromancerDeathKillsSkeleton

验证死灵法师死亡时，其召唤的骷髅立即死亡。
此 spec 验证基础死亡链路；联动死亡验证需要 Unity PlayMode 测试。

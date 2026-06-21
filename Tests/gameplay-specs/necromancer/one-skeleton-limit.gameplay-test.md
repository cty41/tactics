---
feature: Necromancer
scenario: OneSkeletonLimit
tags:
  - necromancer
  - summon
  - skeleton
  - limit
requiredAdapters:
  - Battle
setup:
  - kind: bindBattleController
    parameters: {}
actions:
  - kind: killUnit
    parameters:
      unitAlias: p2_0
  - kind: killUnit
    parameters:
      unitAlias: p2_1
assertions:
  - kind: unitIsCorpse
    target: p2_0
    expected: true
    parameters: {}
  - kind: unitIsCorpse
    target: p2_1
    expected: true
    parameters: {}
timeoutMs: 10000
---

# Necromancer - OneSkeletonLimit

验证同一死灵法师同时最多只能有 1 个骷髅。
此 spec 验证多尸体生成；召唤上限验证需要完整 AbilityConfig 资产。

---
feature: Necromancer
scenario: SkeletonDelayedAction
tags:
  - necromancer
  - summon
  - skeleton
  - turn
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
  - kind: currentRoundEquals
    expected: 0
    parameters: {}
timeoutMs: 10000
---

# Necromancer - SkeletonDelayedAction

验证骷髅在召唤当轮不能行动，从下一轮开始加入轮转。
此 spec 验证初始状态；完整的骷髅行动验证需要 Unity PlayMode 测试。

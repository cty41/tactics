---
feature: Battle
scenario: AIEngageThenAttack
tags: [battle, ai, engage, attack]
requiredAdapters: [Battle]
setup:
  - kind: bindBattleController
    adapter: Battle
    parameters: {}
  - kind: createAiBrain
    adapter: Battle
    parameters: { brainAssetAlias: attack_brain, brainType: attack }
actions:
  - kind: moveUnit
    adapter: Battle
    parameters: { unitAlias: p2_0, cellAlias: cell_3_0 }
  - kind: executeAI
    adapter: Battle
    parameters: { unitAlias: p1_0, brainAssetAlias: attack_brain, targetAlias: p2_0 }
assertions:
  - kind: aiWasNoOpEquals
    adapter: Battle
    expected: false
    parameters: {}
  - kind: unitPositionChangedSinceStep
    adapter: Battle
    expected: true
    parameters: {}
  - kind: targetHealthChangedSinceStep
    adapter: Battle
    expected: true
    parameters: {}
timeoutMs: 15000
---

# Battle - AI Engage Then Attack

敌人位于近战范围外时，AI 先移动到合法攻击位，再在同次接敌意图中完成一次攻击。

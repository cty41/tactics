---
feature: Battle
scenario: BattleAiDecision
tags:
  - battle
  - ai
requiredAdapters:
  - Battle
setup:
  - kind: bindBattleController
    parameters: {}
  - kind: createAiBrain
    parameters:
      brainAssetAlias: attackBrain
      brainType: attack
actions:
  - kind: advanceTurn
    parameters: {}
  - kind: executeAI
    parameters:
      unitAlias: p1_0
      brainAssetAlias: attackBrain
assertions:
  - kind: battleIsActive
    expected: true
    parameters: {}
  - kind: currentRoundEquals
    expected: 1
    parameters: {}
timeoutMs: 15000
---

# Battle - BattleAiDecision

AI 决策回归：创建攻击型 AI 脑，推进回合后执行 AI 决策，验证战斗状态和回合数。

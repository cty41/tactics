---
feature: Battle
scenario: BattleAiTurnResult
tags:
  - battle
  - ai
  - turn-result
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
  - kind: executeAI
    parameters:
      unitAlias: p1_0
      brainAssetAlias: attackBrain
assertions:
  - kind: aiTurnSucceededEquals
    expected: true
    parameters: {}
  - kind: aiTurnUsedFallbackEquals
    expected: false
    parameters: {}
timeoutMs: 15000
---

# Battle - BattleAiTurnResult

执行一次 AI 回合，并通过结构化结果验证执行状态与是否走了回退路径。

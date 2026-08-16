---
feature: Map
scenario: EncounterRuntimeContract
tags:
  - pure-run
  - encounter
  - ai
requiredAdapters:
  - Map
setup:
  - kind: setRunSeed
    parameters:
      seed: 417
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
actions:
  - kind: setRunSeed
    parameters:
      seed: 417
assertions:
  - kind: encounterRecipeContract
    target: E1
    expected:
      healthMultiplier: 1.3
      outputMultiplier: 1.15
      blockedCell: 4,4
    parameters: {}
  - kind: encounterRecipeContract
    target: E2
    expected:
      healthMultiplier: 1.3
      outputMultiplier: 1.15
    parameters: {}
  - kind: encounterRecipeContract
    target: Special
    expected:
      healthMultiplier: 1.8
      outputMultiplier: 1.25
    parameters: {}
  - kind: monsterAiCatalogValid
    expected: true
    parameters: {}
  - kind: battleDefeatRewardsAreZero
    expected: true
    parameters: {}
timeoutMs: 10000
---

# Map - EncounterRuntimeContract

验证 Pure Run 遭遇倍率、中心阻挡格、六类怪物独立 AI 资产、远程技能资源和战败零奖励契约。

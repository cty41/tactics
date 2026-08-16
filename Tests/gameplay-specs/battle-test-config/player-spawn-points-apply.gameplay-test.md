---
feature: BattleTestConfig
scenario: PlayerSpawnPointsApplyCorrectly
tags:
  - battle
  - test-config
  - spawn-point
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createCell
    parameters:
      alias: playerSpawnA
      x: 0
      'y': 0
  - kind: createCell
    parameters:
      alias: playerSpawnB
      x: 0
      'y': 1
  - kind: loadTestPartyConfig
    parameters:
      configPath: Tests/gameplay-specs/battle-test-config/Assets/Party/TestParty.asset
actions:
  - kind: setBattleTestMode
    parameters:
      enabled: true
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: stepMessageContains
    expected: loadTestPartyConfig
    parameters: {}
  - kind: stepMessageContains
    expected: setBattleTestMode
    parameters: {}
timeoutMs: 10000
---

# BattleTestConfig - PlayerSpawnPointsApplyCorrectly

验证玩家测试配置可按坐标语义加载。

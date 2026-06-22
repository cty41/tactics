---
feature: BattleTestConfig
scenario: MissingSpawnPointId_FailsClearly
tags:
  - battle
  - test-config
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createCell
    parameters:
      alias: dummySpawn
      x: 0
      'y': 0
  - kind: loadTestPartyConfig
    parameters:
      configPath: Tests/gameplay-specs/battle-test-config/Assets/Party/TestParty.asset
      spawnPointPrefix: missing_spawn
actions:
  - kind: setBattleTestMode
    parameters:
      enabled: true
assertions:
  - kind: executionStateEquals
    expected: Failed
    parameters: {}
  - kind: stepMessageContains
    expected: loadTestPartyConfig
    parameters: {}
timeoutMs: 10000
---

# BattleTestConfig - MissingSpawnPointId_FailsClearly

验证配置指向不存在的 spawn 点位前缀时，测试链路会以明确失败提示，而不是静默继续。

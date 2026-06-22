---
feature: BattleTestConfig
scenario: MissingPartyConfig_FailsClearly
tags:
  - battle
  - test-config
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: loadTestPartyConfig
    parameters:
      configPath: Tests/gameplay-specs/battle-test-config/Assets/Party/TestParty.asset
      spawnPointPrefix: missing_player_spawn
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

# BattleTestConfig - MissingPartyConfig_FailsClearly

验证当玩家测试配置指向不存在的 spawn 点位时，测试链路会以明确失败提示，而不是静默继续。

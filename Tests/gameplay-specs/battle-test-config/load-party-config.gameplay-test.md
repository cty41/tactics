---
feature: BattleTestConfig
scenario: LoadPartyConfig_SetsPartySource
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
      spawnPointPrefix: player_spawn
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

# BattleTestConfig - LoadPartyConfig_SetsPartySource

验证 `loadTestPartyConfig` 可加载玩家测试配置来源，并能切换到测试模式。

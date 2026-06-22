---
feature: BattleTestConfig
scenario: DisableTestMode_FallsBackToDefaultLoading
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
  - kind: loadTestEncounterConfig
    parameters:
      configPath: Tests/gameplay-specs/battle-test-config/Assets/Encounter/TestEncounter.asset
      spawnPointPrefix: enemy_spawn
actions:
  - kind: setBattleTestMode
    parameters:
      enabled: true
  - kind: setBattleTestMode
    parameters:
      enabled: false
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: stepMessageContains
    expected: setBattleTestMode
    parameters: {}
timeoutMs: 10000
---

# BattleTestConfig - DisableTestMode_FallsBackToDefaultLoading

验证可先开启测试模式再关闭，且流程不会失败。

---
feature: BattleTestConfig
scenario: LoadEncounterConfig_SetsEncounterSource
tags:
  - battle
  - test-config
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: loadTestEncounterConfig
    parameters:
      configPath: Tests/gameplay-specs/battle-test-config/Assets/Encounter/TestEncounter.asset
      spawnPointPrefix: enemy_spawn
actions:
  - kind: setBattleTestMode
    parameters:
      enabled: true
assertions:
  - kind: executionStateEquals
    expected: Completed
    parameters: {}
  - kind: stepMessageContains
    expected: loadTestEncounterConfig
    parameters: {}
  - kind: stepMessageContains
    expected: setBattleTestMode
    parameters: {}
timeoutMs: 10000
---

# BattleTestConfig - LoadEncounterConfig_SetsEncounterSource

验证 `loadTestEncounterConfig` 可加载敌方测试关卡配置来源，并能切换到测试模式。

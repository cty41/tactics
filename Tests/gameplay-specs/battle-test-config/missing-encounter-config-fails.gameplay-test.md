---
feature: BattleTestConfig
scenario: MissingEncounterConfig_FailsClearly
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
      configPath: ""
      spawnPointPrefix: enemy_spawn
actions:
  - kind: setBattleTestMode
    parameters:
      enabled: true
assertions:
  - kind: executionStateEquals
    expected: Failed
    parameters: {}
  - kind: stepMessageContains
    expected: loadTestEncounterConfig
    parameters: {}
timeoutMs: 10000
---

# BattleTestConfig - MissingEncounterConfig_FailsClearly

验证缺少敌方测试配置路径时，链路会明确失败。

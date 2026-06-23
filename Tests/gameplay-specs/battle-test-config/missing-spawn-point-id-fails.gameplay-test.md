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
      alias: dummyCell
      x: 0
      'y': 0
  - kind: loadTestPartyConfig
    parameters:
      configPath: ""
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

# BattleTestConfig - MissingPartyConfigPath_FailsClearly

验证缺少玩家测试配置路径时，测试链路会以明确失败提示，而不是静默继续。

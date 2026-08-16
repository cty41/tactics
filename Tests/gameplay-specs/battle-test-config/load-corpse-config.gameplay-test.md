---
feature: BattleTestConfig
scenario: LoadCorpseConfig_SetsEncounterSource
tags:
  - battle
  - test-config
  - corpse
requiredAdapters:
  - Skill
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: loadTestEncounterConfig
    parameters:
      configPath: Assets/Tactics/ScriptableObjects/BattleTest/CorpseTestEncounter.asset
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

# BattleTestConfig - LoadCorpseConfig_SetsEncounterSource

验证 `loadTestEncounterConfig` 可加载包含 corpse slots 的敌方测试关卡配置，并能切换到测试模式。
覆盖 config loading 链路，确保含尸体槽位的 encounter config 可正常被 test setup 消费。

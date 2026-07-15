---
feature: Map
scenario: RunSeedGrowthAssertions
tags:
  - map
  - run
  - growth
requiredAdapters:
  - Map
setup:
  - kind: setRunSeed
    parameters:
      seed: 20260714
  - kind: loadRoguelikeMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
      strictAsset: true
actions:
  - kind: setRosterCharacterState
    target: mage
    parameters:
      characterId: mage
      level: 3
      skillId: mage_fireball_1
      skillLevel: 2
assertions:
  - kind: mapIsActive
    expected: true
    parameters: {}
  - kind: rosterCharacterLevelEquals
    target: mage
    expected: 3
    parameters: {}
  - kind: rosterCharacterHasSkillId
    target: mage
    expected: mage_fireball_1
    parameters: {}
timeoutMs: 15000
---

# Map - RunSeedGrowthAssertions

固定 single run 随机种子，严格加载指定地图资产，并验证角色等级与已学习 SkillId 的写回结果。

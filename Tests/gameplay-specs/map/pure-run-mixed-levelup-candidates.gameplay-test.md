---
feature: Map
scenario: PureRunMixedLevelUpCandidates
tags:
  - map
  - pure-run
  - growth
  - skill-upgrade
requiredAdapters:
  - Map
setup:
  - kind: setRunSeed
    parameters:
      seed: 20260722
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
actions:
  - kind: setRosterCharacterState
    adapter: Map
    parameters:
      characterId: pure_run_mage
      level: 2
      skillId: mage.fireball
      skillLevel: 1
assertions:
  - kind: mapIsActive
    expected: true
    parameters: {}
  - kind: rosterCharacterSkillLevelEquals
    target: pure_run_mage
    expected: 1
    parameters:
      skillId: mage.fireball
  - kind: pureRunSkillChoiceContains
    target: pure_run_mage
    expected: mage.fireball
    parameters:
      targetLevel: 2
  - kind: pureRunSkillChoicesAreMixed
    target: pure_run_mage
    expected: true
    parameters: {}
timeoutMs: 15000
---

# Map - Pure Run Mixed Level-Up Candidates

验证角色 Lv2 时，稳定技能 ID 的 Fireball Lv2 升级与新 Lv1 技能同时进入确定性候选池。

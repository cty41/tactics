---
feature: UI
scenario: LevelUpMixedCandidateConfirmation
tags: [ui, pure-run, levelup]
requiredAdapters: [Map, UI]
setup:
  - kind: setRunSeed
    parameters: { seed: 20260722 }
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
      attributePoints: 1
  - kind: openUI
    adapter: UI
    parameters: { uiId: LevelUp }
  - kind: configureLevelUpPanel
    adapter: UI
    parameters: { characterId: pure_run_mage }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: AttributePlus_Strength }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: LevelUpSkillCard_mage_fireball }
assertions:
  - kind: elementText
    adapter: UI
    target: LevelUpSkillLevel_mage_fireball
    expected: Lv.2
    parameters: {}
  - kind: elementEnabled
    adapter: UI
    target: ConfirmButton
    expected: true
    parameters: {}
timeoutMs: 15000
---

# UI - LevelUp Mixed Candidate Confirmation

混合候选显示真实目标等级，且只有属性点合法分配并选择候选后才允许显式确认。

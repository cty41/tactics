---
feature: Map
scenario: PureRunRealPlayerRoute
tags: [pure-run, journey-integration, ui, battle, levelup, store, reentry, boss]
requiredAdapters: [UI, Map, Battle, Skill]
setup:
  - kind: bindBattleController
    adapter: Battle
    parameters: {}
  - kind: createSkillGraph
    adapter: Skill
    parameters:
      alias: playerFinisher
      graphKind: singleTargetDamage
      baseDamage: 999
      isRanged: false
      minRange: 1
      maxRange: 1
actions:
  - kind: openUI
    adapter: UI
    parameters: { uiId: Home }
  - kind: waitForElement
    adapter: UI
    parameters: { elementName: NewGameButton, minimumFrames: 2, maxFrames: 30 }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: NewGameButton }
  # 开局初始技能自选：依次为 Mage / Necromancer / Amazon 选第 1 个三系技能并确认
  # 每次连点两下 SkillOption_0（选择幂等，吸收 UI 重建时的首击丢失）
  - kind: waitForElement
    adapter: UI
    parameters: { elementName: SkillOption_0, minimumFrames: 3, maxFrames: 60 }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: SkillOption_0 }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: SkillOption_0 }
  - kind: waitForElement
    adapter: UI
    parameters: { elementName: ConfirmButton, minimumFrames: 2, maxFrames: 30 }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: ConfirmButton }
  - kind: waitForElement
    adapter: UI
    parameters: { elementName: SkillOption_0, minimumFrames: 3, maxFrames: 60 }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: SkillOption_0 }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: SkillOption_0 }
  - kind: waitForElement
    adapter: UI
    parameters: { elementName: ConfirmButton, minimumFrames: 2, maxFrames: 30 }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: ConfirmButton }
  - kind: waitForElement
    adapter: UI
    parameters: { elementName: SkillOption_0, minimumFrames: 3, maxFrames: 60 }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: SkillOption_0 }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: SkillOption_0 }
  - kind: waitForElement
    adapter: UI
    parameters: { elementName: ConfirmButton, minimumFrames: 2, maxFrames: 30 }
  - kind: clickElement
    adapter: UI
    parameters: { elementName: ConfirmButton }
  - kind: waitForMapReady
    adapter: UI
    parameters: {}
  - kind: captureActivePureRun
    adapter: Map
    parameters: {}

  - kind: beginBattleNode
    adapter: Map
    parameters: { nodeId: layer_01_battle }
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters: { graphAlias: playerFinisher, casterAlias: p1_0, targetAlias: p2_0 }
  - kind: waitForBattleEnd
    adapter: Battle
    parameters: { maxFrames: 120 }
  - kind: commitNaturalBattleVictory
    adapter: Map
    parameters: { playerNumber: 1 }
  - kind: grantPureRunLevel
    adapter: Map
    parameters: { characterId: pure_run_mage }
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
  - kind: clickElement
    adapter: UI
    parameters: { elementName: ConfirmButton }
  - kind: bindPureRunAbilityToUnit
    adapter: Battle
    parameters: { unitAlias: p1_0, skillId: mage.fireball, level: 2 }

  - kind: spawnBattleUnit
    adapter: Battle
    parameters: { alias: enemy_02, cellAlias: cell_1_0, playerNumber: 2, health: 8, maxHealth: 8 }
  - kind: restartBattle
    adapter: Battle
    parameters: {}
  - kind: beginBattleNode
    adapter: Map
    parameters: { nodeId: layer_02_battle }
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters: { graphAlias: playerFinisher, casterAlias: p1_0, targetAlias: enemy_02 }
  - kind: waitForBattleEnd
    adapter: Battle
    parameters: { maxFrames: 120 }
  - kind: commitNaturalBattleVictory
    adapter: Map
    parameters: { playerNumber: 1 }

  - kind: spawnBattleUnit
    adapter: Battle
    parameters: { alias: enemy_03, cellAlias: cell_1_0, playerNumber: 2, health: 8, maxHealth: 8 }
  - kind: restartBattle
    adapter: Battle
    parameters: {}
  - kind: beginBattleNode
    adapter: Map
    parameters: { nodeId: layer_03_battle }
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters: { graphAlias: playerFinisher, casterAlias: p1_0, targetAlias: enemy_03 }
  - kind: waitForBattleEnd
    adapter: Battle
    parameters: { maxFrames: 120 }
  - kind: commitNaturalBattleVictory
    adapter: Map
    parameters: { playerNumber: 1 }

  - kind: setAdventureGold
    adapter: Map
    parameters: { amount: 100 }
  - kind: buyShopGoodTransaction
    adapter: Map
    parameters: { nodeId: layer_04_store, itemKind: Consumable, contentId: life_potion, price: 10 }
  - kind: commitNodeTransaction
    adapter: Map
    parameters: { nodeId: layer_04_store, consumeNode: false }
  - kind: commitNodeInteraction
    adapter: Map
    parameters: { nodeId: layer_04_store }
  - kind: reloadPureRunSession
    adapter: Map
    parameters: {}

  - kind: spawnBattleUnit
    adapter: Battle
    parameters: { alias: enemy_05, cellAlias: cell_1_0, playerNumber: 2, health: 8, maxHealth: 8 }
  - kind: restartBattle
    adapter: Battle
    parameters: {}
  - kind: beginBattleNode
    adapter: Map
    parameters: { nodeId: layer_05_battle }
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters: { graphAlias: playerFinisher, casterAlias: p1_0, targetAlias: enemy_05 }
  - kind: waitForBattleEnd
    adapter: Battle
    parameters: { maxFrames: 120 }
  - kind: commitNaturalBattleVictory
    adapter: Map
    parameters: { playerNumber: 1 }

  - kind: buyShopGoodTransaction
    adapter: Map
    parameters: { nodeId: layer_06_store, itemKind: Consumable, contentId: mana_potion, price: 10 }
  - kind: commitNodeTransaction
    adapter: Map
    parameters: { nodeId: layer_06_store, consumeNode: false }
  - kind: commitNodeInteraction
    adapter: Map
    parameters: { nodeId: layer_06_store }

  - kind: spawnBattleUnit
    adapter: Battle
    parameters: { alias: boss_07, cellAlias: cell_1_0, playerNumber: 2, health: 8, maxHealth: 8 }
  - kind: restartBattle
    adapter: Battle
    parameters: {}
  - kind: beginBattleNode
    adapter: Map
    parameters: { nodeId: layer_07_special }
  - kind: executeBattleSkillGraph
    adapter: Battle
    parameters: { graphAlias: playerFinisher, casterAlias: p1_0, targetAlias: boss_07 }
  - kind: waitForBattleEnd
    adapter: Battle
    parameters: { maxFrames: 120 }
  - kind: commitNaturalBattleVictory
    adapter: Map
    parameters: { playerNumber: 1 }
assertions:
  - kind: actualSkillLevelEquals
    adapter: Battle
    target: p1_0
    expected: 2
    parameters: { skillId: mage.fireball }
  - kind: completedSummaryOutcomeEquals
    adapter: Map
    expected: Victory
    parameters: {}
  - kind: completedSummaryNodesVisitedEquals
    adapter: Map
    expected: 7
    parameters: {}
  - kind: completedSummaryContainsItem
    adapter: Map
    expected: life_potion
    parameters: {}
timeoutMs: 45000
---

# Map - Pure Run Journey Integration

快速跨系统集成回归：从 Home 的 New Run 按钮开始，以真实战斗伤害触发五次胜利，完成法师火球 Lv2 的显式升级确认，经过商店购买和会话重载，最终击败 Boss 并验证胜利 RunSummary。该用例保留直接适配器操作，不宣称覆盖真实玩家输入。

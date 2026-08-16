---
feature: SharedBattlePrimitives
scenario: FacingAndInitiative
tags:
  - battle
  - facing
  - initiative
requiredAdapters:
  - Skill
  - Battle
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createCell
    parameters: { alias: actorCell, x: 0, y: 0 }
  - kind: createCell
    parameters: { alias: northCell, x: 0, y: 1 }
  - kind: createCell
    parameters: { alias: targetCell, x: 2, y: 0 }
  - kind: createCell
    parameters: { alias: thirdCell, x: 3, y: 0 }
  - kind: createUnit
    parameters: { alias: actor, playerNumber: 0, cellAlias: actorCell, speed: 8 }
  - kind: createUnit
    parameters: { alias: target, playerNumber: 1, cellAlias: targetCell, speed: 6 }
  - kind: createUnit
    parameters: { alias: third, playerNumber: 1, cellAlias: thirdCell, speed: 5 }
actions:
  - kind: setUnitFacing
    parameters: { unitAlias: actor, facing: East }
  - kind: initializeInitiativeOrder
    parameters:
      unitAliases: [actor, target, third]
  - kind: advanceInitiative
    parameters: {}
  - kind: addBuff
    parameters:
      unitAlias: target
      buffName: Slow
      configAlias: shared_slow
      duration: 2
      effectType: Slow
      speedModifier: -2
      refreshStrategy: RefreshDuration
  - kind: moveUnit
    parameters: { unitAlias: actor, cellAlias: northCell }
assertions:
  - kind: unitFacingEquals
    target: actor
    expected: North
    parameters: {}
  - kind: currentRoundOrderEquals
    expected: [actor, third, target]
    parameters: {}
  - kind: unitStatusRemainingActionsEquals
    target: target
    expected: 2
    parameters: { buffName: Slow }
timeoutMs: 10000
---

# Shared Battle Primitives - Facing and Initiative

验证成功移动更新四方向状态，Slow 立即重排当前轮尚未行动的单位，已行动单位不会重复进入队列。

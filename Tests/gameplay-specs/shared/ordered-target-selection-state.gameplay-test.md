---
feature: SharedBattlePrimitives
scenario: OrderedTargetSelectionState
tags:
  - battle
  - targeting
  - multi-stage
requiredAdapters:
  - Skill
  - Battle
setup:
  - kind: createSkillTestWorld
    parameters: {}
  - kind: createCell
    parameters: { alias: aCell, x: 0, y: 0 }
  - kind: createCell
    parameters: { alias: bCell, x: 1, y: 0 }
  - kind: createUnit
    parameters: { alias: targetA, playerNumber: 1, cellAlias: aCell }
  - kind: createUnit
    parameters: { alias: targetB, playerNumber: 1, cellAlias: bCell }
actions:
  - kind: beginOrderedTargetSelection
    parameters: { requiredCount: 3 }
  - kind: selectOrderedTarget
    parameters: { targetAlias: targetA }
  - kind: selectOrderedTarget
    parameters: { targetAlias: targetB }
  - kind: selectOrderedTarget
    parameters: { targetAlias: targetA }
  - kind: undoOrderedTargetSelection
    parameters: {}
  - kind: selectOrderedTarget
    parameters: { targetAlias: targetB }
  - kind: commitOrderedTargetSelection
    parameters: {}
assertions:
  - kind: orderedTargetSelectionEquals
    expected: [targetA, targetB, targetB]
    parameters: {}
  - kind: selectionStageEquals
    expected: Committed
    parameters: {}
timeoutMs: 10000
---

# Shared Battle Primitives - Ordered Target Selection State

验证有序多段选择允许重复目标、撤销最后一段并在满足段数后原序提交。

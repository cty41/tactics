---
feature: InteractableCorpse
scenario: ConsumeInteractableCorpse
tags:
  - battle
  - interactable
  - corpse
requiredAdapters:
  - Battle
setup:
  - kind: bindBattleController
    parameters: {}
  - kind: createCell
    parameters:
      alias: cell_0_0
      x: 0
      "y": 0
actions:
  - kind: spawnInteractableCorpse
    parameters:
      cellAlias: cell_0_0
  - kind: consumeInteractableCorpseAt
    parameters:
      cellAlias: cell_0_0
assertions:
  - kind: interactableCorpseExistsAt
    target: cell_0_0
    expected: false
    parameters: {}
  - kind: cellOccupiedByInteractable
    target: cell_0_0
    expected: false
    parameters: {}
timeoutMs: 10000
---

# InteractableCorpse - ConsumeInteractableCorpse

验证 `consumeInteractableCorpseAt` 能消耗格子上的尸体对象，之后该格子不再被 interactable 占用。

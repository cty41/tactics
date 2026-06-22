---
feature: InteractableCorpse
scenario: NoInteractableCorpseAtEmptyCell
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
      alias: cell_1_1
      x: 1
      "y": 1
actions:
  - kind: advanceTurn
    parameters: {}
assertions:
  - kind: interactableCorpseExistsAt
    target: cell_1_1
    expected: false
    parameters: {}
  - kind: cellOccupiedByInteractable
    target: cell_1_1
    expected: false
    parameters: {}
timeoutMs: 10000
---

# InteractableCorpse - NoInteractableCorpseAtEmptyCell

验证未放置尸体的格子上不存在 interactable corpse，且不被 interactable 占用。

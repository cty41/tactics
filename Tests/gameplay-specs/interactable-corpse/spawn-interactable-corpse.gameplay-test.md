---
feature: InteractableCorpse
scenario: SpawnInteractableCorpse
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
assertions:
  - kind: interactableCorpseExistsAt
    target: cell_0_0
    expected: true
    parameters: {}
  - kind: cellOccupiedByInteractable
    target: cell_0_0
    expected: true
    parameters: {}
timeoutMs: 10000
---

# InteractableCorpse - SpawnInteractableCorpse

验证 `spawnInteractableCorpse` 能在指定格子生成一个独立战场尸体对象，且该格子被标记为占用。

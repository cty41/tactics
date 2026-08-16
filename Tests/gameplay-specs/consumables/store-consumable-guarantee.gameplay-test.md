---
feature: Map
scenario: StoreConsumableGuarantee
tags:
  - map
  - store
  - consumable
requiredAdapters:
  - Map
setup:
  - kind: setRunSeed
    parameters:
      seed: 20260717
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - kind: setAdventureGold
    parameters:
      amount: 10
actions:
  - kind: buyShopGood
    parameters:
      itemKind: Consumable
      contentId: life_potion
      price: 5
assertions:
  - kind: shopGoodCountEquals
    expected: 3
    parameters:
      nodeId: deterministic-store
  - kind: shopConsumableCountAtLeast
    expected: 1
    parameters:
      nodeId: deterministic-store
  - kind: shopConsumableIdsUnique
    expected: true
    parameters:
      nodeId: deterministic-store
  - kind: runGoldEquals
    expected: 5
    parameters: {}
  - kind: backpackConsumableCountEquals
    expected: 1
    parameters:
      definitionId: life_potion
timeoutMs: 10000
---

# Map - StoreConsumableGuarantee

确定性商店始终生成三件商品、至少一瓶药水且药水 ID 不重复；购买药水后金币和共享背包立即更新。

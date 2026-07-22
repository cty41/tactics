---
feature: Map
scenario: StorePurchaseReentry
tags:
  - pure-run
  - store
  - transaction
requiredAdapters:
  - Map
setup:
  - kind: setRunSeed
    parameters:
      seed: 7303
  - kind: loadPureRunMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset
  - kind: setAdventureGold
    parameters:
      amount: 100
actions:
  - kind: buyShopGoodTransaction
    adapter: Map
    parameters:
      nodeId: layer_04_store
      itemKind: Consumable
      contentId: life_potion
      price: 10
  - kind: reloadPureRunSession
    adapter: Map
    parameters: {}
  - kind: buyShopGoodTransaction
    adapter: Map
    parameters:
      nodeId: layer_04_store
      itemKind: Consumable
      contentId: life_potion
      price: 10
assertions:
  - kind: runGoldEquals
    adapter: Map
    expected: 40
    parameters: {}
  - kind: consumableCountEquals
    adapter: Map
    expected: 1
    parameters:
      consumableId: life_potion
  - kind: transactionApplicationCountEquals
    adapter: Map
    expected: 1
    parameters:
      key: node:layer_04_store:purchase:item:life_potion
  - kind: nodeTransactionPhaseEquals
    adapter: Map
    target: layer_04_store
    expected: Entered
    parameters: {}
timeoutMs: 10000
---

# Map - StorePurchaseReentry

购买后中断再进入商店不会重复扣款或重复发放同一商品，购买事务键持久化为唯一事实。

# Roguelike 节点资源更新计划 (使用 MCP 工具)

## 任务摘要
使用 MCP 工具直接修改 `RoguelikeNodeBlueprint` 资源的 `sprite` 和 `nodeType` 字段。

## 发现的信息
通过 `assets-find` 找到的 Texture2D 资源(可能是 Sprite 的容器):
| Node Type | Texture2D Path | instanceID |
|-----------|----------------|------------|
| EliteEnemy | Assets/Tactics/Arts/Sprites/game-icons.net/delapouite/brute.png | 57256 |
| MinorEnemy | Assets/Tactics/Arts/Sprites/game-icons.net/delapouite/bully-minion.png | 57264 |
| RestSite | Assets/Tactics/Arts/Sprites/game-icons.net/lorc/campfire.png | 57272 |
| Treasure | Assets/Tactics/Arts/Sprites/game-icons.net/delapouite/chest.png | 57288 |
| Store | Assets/Tactics/Arts/Sprites/game-icons.net/delapouite/backpack.png | 57284 |
| Mystery | Assets/Tactics/Arts/Sprites/game-icons.net/lorc/letter-bomb.png | 57268 |
| Boss(Executioner) | Assets/Tactics/Arts/Sprites/game-icons.net/delapouite/executioner-hood.png | 57260 |
| Boss(Skeleton) | Assets/Tactics/Arts/Sprites/game-icons.net/lorc/crowned-skull.png | 57276 |
| Boss(Spider) | Assets/Tactics/Arts/Sprites/game-icons.net/carl-olsen/spider-face.png | 57280 |

## RoguelikeNodeType 枚举值
```
MinorEnemy = 0
EliteEnemy = 1
RestSite = 2
Treasure = 3
Store = 4
Boss = 5
Mystery = 6
```

## 当前 RoguelikeNodeBlueprint 资源状态
通过 `assets-get-data` 获取:
- **EliteEnemy.asset**: nodeType=0 (应该是1), sprite={instanceID:0} (需要设置)
- **MinorEnemy.asset**: nodeType=0 (正确), sprite={instanceID:0} (需要设置)
- **RestSite.asset**: nodeType=0 (应该是2), sprite={instanceID:0} (需要设置)
- **Treasure.asset**: nodeType=0 (应该是3), sprite={instanceID:0} (需要设置)
- **Store.asset**: nodeType=0 (应该是4), sprite={instanceID:0} (需要设置)
- **Mystery.asset**: nodeType=0 (应该是6), sprite={instanceID:0} (需要设置)
- **ExecutionerBoss.asset**: nodeType=0 (应该是5), sprite={instanceID:0} (需要设置)
- **SkeletonBoss.asset**: nodeType=0 (应该是5), sprite={instanceID:0} (需要设置)
- **SpiderBoss.asset**: nodeType=0 (应该是5), sprite={instanceID:0} (需要设置)

## 执行步骤
1. 使用 `object-modify` 工具修改每个 RoguelikeNodeBlueprint 资源:
   - 设置 `nodeType` 字段为正确的枚举值(int)
   - 设置 `sprite` 字段为对应的 Texture2D instanceID

## object-modify 示例
对于 EliteEnemy.asset (instanceID 从 assets-get-data 获取):
```json
{
  "objectRef": {"instanceID": <instanceID>},
  "objectDiff": {
    "typeName": "Tactics.RoguelikeMap.RoguelikeNodeBlueprint",
    "fields": [
      {"typeName": "UnityEngine.Sprite", "name": "sprite", "value": {"instanceID": 57256}},
      {"typeName": "Tactics.RoguelikeMap.RoguelikeNodeType", "name": "nodeType", "value": 1}
    ]
  }
}
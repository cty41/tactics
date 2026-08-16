---
feature: Map
scenario: MapEventNode
tags:
  - map
  - event
requiredAdapters:
  - Map
setup:
  - kind: loadRoguelikeMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
      useFallbackMap: true
actions:
  - kind: enterNode
    adapter: Map
    parameters:
      nodeId: mystery_node_1
  - kind: triggerEvent
    adapter: Map
    parameters:
      eventId: cursed_chest_001
  - kind: completeNode
    adapter: Map
    parameters:
      nodeId: mystery_node_1
assertions:
  - kind: mapIsActive
    expected: true
    parameters: {}
  - kind: nodeIsVisited
    target: mystery_node_1
    expected: true
    parameters: {}
  - kind: visitedNodeCountEquals
    expected: 1
    parameters: {}
timeoutMs: 15000
---

# Map - MapEventNode

事件节点回归：加载地图，进入神秘节点，触发事件，完成节点，验证地图状态和访问计数。

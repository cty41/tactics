---
feature: Map
scenario: MapEnterNode
tags:
  - map
  - roguelike
requiredAdapters:
  - Map
setup:
  - kind: loadRoguelikeMap
    parameters:
      mapConfigPath: Assets/Tactics/RoguelikeMap/MapConfigs/DefaultMapConfig.asset
actions:
  - kind: enterNode
    parameters:
      nodeId: start_node
  - kind: completeNode
    parameters:
      nodeId: start_node
assertions:
  - kind: mapIsActive
    expected: true
    parameters: {}
  - kind: visitedNodeCountEquals
    expected: 1
    parameters: {}
  - kind: nodeIsVisited
    target: start_node
    expected: true
    parameters: {}
timeoutMs: 15000
---

# Map - MapEnterNode

最小 Map adapter 回归：加载地图，进入起始节点，完成节点，验证地图状态和访问计数。
